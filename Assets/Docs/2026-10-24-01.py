"""Local-only editor: serves one HTML page and saves its adjacent JSON record."""
import hashlib
import copy
import math
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
from http.server import HTTPServer, BaseHTTPRequestHandler
import sys
import socket
import webbrowser
from urllib.request import urlopen

BASE = Path(__file__).resolve().parent
HTML = BASE / '2026-10-24-01.html'
DATA = BASE / '2026-10-24-01.json'
HOST, PORT = '127.0.0.1', 18764
ORIGIN = f'http://{HOST}:{PORT}'
API_VERSION = 3
SERVICE_REVISION = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()

class ReviewServer(HTTPServer):
    # Windows SO_REUSEADDR permits two listeners, allowing an obsolete writer to keep answering.
    allow_reuse_address = False
    def server_bind(self):
        if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        super().server_bind()
seed = json.loads(re.search(r'<script id="seed" type="application/json">(.*?)</script>', HTML.read_text(encoding='utf-8'), re.S).group(1))

def contents():
    return DATA.read_bytes() if DATA.exists() else json.dumps(seed, ensure_ascii=False).encode('utf-8')

def tag(data):
    return '"' + hashlib.sha256(data).hexdigest() + '"'

def camera_parameters():
    names = ['LightOn', 'LightOff', 'DoorOn', 'DoorOff', 'LightMove', 'DoorMove', 'WorldMove', 'LightWorldMove', 'LightDoorMove', 'LightLightMove', 'DoorWorldMove', 'DoorLightMove', 'WorldLightMove', 'WorldDoorMove']
    profiles = {}
    for index, name in enumerate(names, 3):
        source = BASE.parent / 'GameData/V4/Opening/IntroShots' / (name + '.asset')
        text = source.read_text(encoding='utf-8')
        def number(key, default):
            match = re.search(r'^  ' + key + r': ([-\d.]+)$', text, re.M)
            return float(match.group(1)) if match else default
        def vector(key):
            match = re.search(r'^  ' + key + r': (.*)$', text, re.M)
            return {k:float(v) for k,v in re.findall(r'(\w): ([-\d.]+)', match.group(1))} if match else {}
        offset, angle = vector('targetOffset'), vector('angleOffset')
        profiles[f'S{index:02}'] = dict(target=['最近灯','门','天空','灯群','地平线'][int(number('target',0))], aim=['对准目标','只调整俯仰','按灯群高度切换','仅俯仰对准目标'][int(number('aim',0))],
            offsetX=offset.get('x',0),offsetY=offset.get('y',0),offsetZ=offset.get('z',0),pitch=angle.get('x',0),yaw=angle.get('y',0),
            minimumElevation=number('skyMinimumElevation',15),heightThreshold=number('elevatedGroupHeight',6),groupRadius=number('lightGroupRadius',18),viewportY=number('targetViewportY',.5),
            follow=bool(number('followTarget',0)),followSpeed=number('maxFollowDegreesPerSecond',12),followResponse=number('followResponse',1.5),snap=number('snapAboveAngle',180)<180,snapAngle=number('snapAboveAngle',180),
            enterSeconds=number('enterDuration',.65),holdSeconds=number('holdDuration',2.4),exitSeconds=number('exitDuration',.65),letterbox=number('letterboxHeight',.09),rollLocked=True,
            heightBasedPitchSpeed=bool(number('heightBasedPitchSpeed',0)),lowPitchSpeed=number('lowPitchSpeed',12),highPitchSpeed=number('highPitchSpeed',20))
    return {'capturedAt':datetime.now(timezone.utc).isoformat(), 'profiles':profiles}

def validate(data):
    if not isinstance(data, dict) or data.get('format') != seed['format'] or data.get('version') != 2:
        raise ValueError('记录格式不正确')
    rows = data.get('rows')
    if not isinstance(rows, list) or len(rows) != len(seed['rows']):
        raise ValueError('条目不完整')
    ids = set()
    for row in rows:
        if not isinstance(row, dict) or row.get('id') not in {r['id'] for r in seed['rows']} or row['id'] in ids:
            raise ValueError('条目标识不正确')
        ids.add(row['id'])
        if row.get('status') not in ['待测', '通过', '暂不复测', '需修改', '未复现'] or row.get('feedback', '待选择') not in ['待选择', '划掉', '涟漪带星', '涟漪']:
            raise ValueError('选项不正确')
        for field in ['sentence', 'worldChange', 'specialRules', 'notes', 'hint']:
            if not isinstance(row.get(field), str) or len(row[field]) > 100000:
                raise ValueError('文字格式不正确')
        validate_behavior(row)
        if 'cases' in row:
            cases = row['cases']
            if not isinstance(cases, list) or not 1 <= len(cases) <= 100:
                raise ValueError('每句需要 1 至 100 种情况')
            case_ids = set()
            for case in cases:
                if not isinstance(case, dict) or not isinstance(case.get('id'), str) or not case['id'] or case['id'] in case_ids:
                    raise ValueError('情况标识不正确')
                case_ids.add(case['id'])
                if case.get('conditionMode') not in ['通用', '自定义条件']:
                    raise ValueError('条件类型不正确')
                for key in ['condition', 'worldChange', 'specialRules']:
                    if not isinstance(case.get(key), str) or len(case[key]) > 100000:
                        raise ValueError('情况描述不正确')
                if case.get('feedback') not in ['待选择', '划掉', '涟漪带星', '涟漪']:
                    raise ValueError('情况反馈不正确')
                validate_behavior(case)

def validate_behavior(row):
    intent = row.get('cameraIntent')
    if intent is not None:
        if not isinstance(intent, dict):
            raise ValueError('镜头意向格式不正确')
        for key in ['targetDescription', 'selectionNotes', 'framingNotes']:
            if not isinstance(intent.get(key), str) or len(intent[key]) > 100000:
                raise ValueError('镜头说明不正确')
        if intent.get('framing') not in ['对准目标', '对准目标群', '只调整俯仰', '保持当前朝向', '自定义'] or intent.get('turn') not in ['平滑转向', '偏转较大时瞬切', '直接瞬切', '自定义'] or type(intent.get('follow')) is not bool:
            raise ValueError('镜头方向选项不正确')
    camera = row.get('camera')
    if camera is not None:
        if row['feedback'] != '涟漪带星' or not isinstance(camera, dict):
            raise ValueError('无演出时不能填写镜头')
        template = seed['cameraTemplates']['S03']
        extra = {'rollLocked','heightBasedPitchSpeed','lowPitchSpeed','highPitchSpeed'}
        if not set(template).issubset(camera) or not set(camera).issubset(set(template)|extra):
            raise ValueError('镜头参数不完整')
        for key, value in camera.items():
            if key == 'target':
                valid = value in ['最近灯', '门', '天空', '灯群', '地平线']
            elif key == 'aim':
                valid = value in ['对准目标', '只调整俯仰', '按灯群高度切换', '仅俯仰对准目标']
            elif isinstance(template.get(key), bool) or key in ['rollLocked','heightBasedPitchSpeed']:
                valid = isinstance(value, bool)
            else:
                valid = type(value) in (int, float) and math.isfinite(value)
                if key in ['enterSeconds', 'holdSeconds', 'exitSeconds', 'groupRadius', 'heightThreshold', 'followResponse', 'followSpeed']:
                    valid = valid and value >= 0
                if key in ['viewportY', 'letterbox']:
                    valid = valid and 0 <= value <= 1
            if not valid:
                raise ValueError('镜头参数不正确：' + key)

def persist(data):
    encoded = json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False).encode('utf-8')
    temporary = DATA.with_suffix('.json.tmp')
    temporary.write_bytes(encoded)
    os.replace(temporary, DATA)
    return encoded

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def respond(self, status, body, mime='application/json; charset=utf-8', etag=None):
        self.send_response(status)
        self.send_header('Content-Type', mime)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        if etag:
            self.send_header('ETag', etag)
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.headers.get('Host') != f'{HOST}:{PORT}':
            return self.respond(403, b'{}')
        if self.path in ['/', '/2026-10-24-01.html']:
            return self.respond(200, HTML.read_bytes(), 'text/html; charset=utf-8')
        if self.path == '/api/record':
            data = contents()
            return self.respond(200, data, etag=tag(data))
        if self.path == '/api/cameras':
            try:
                return self.respond(200, json.dumps(camera_parameters(), ensure_ascii=False).encode('utf-8'))
            except (OSError, ValueError, IndexError) as error:
                return self.respond(500, json.dumps({'error':str(error)}).encode('utf-8'))
        if self.path == '/health':
            return self.respond(200, json.dumps({'editor':'puzzleapple-2026-10-24-01','apiVersion':API_VERSION,'revision':SERVICE_REVISION}).encode('utf-8'))
        self.respond(404, b'{}')

    def do_PUT(self):
        self.mutate(False)

    def do_POST(self):
        self.mutate(True)

    def mutate(self, transition):
        paths = ['/api/submit', '/api/next', '/api/validate'] if transition else ['/api/record']
        if self.path not in paths or self.headers.get('Host') != f'{HOST}:{PORT}' or self.headers.get('Origin') != ORIGIN or self.headers.get('X-Review-Editor') != '1':
            return self.respond(403, b'{}')
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 4000000:
                raise ValueError('记录大小不正确')
            data = json.loads(self.rfile.read(length))
            if self.path == '/api/validate':
                validate(data)
                return self.respond(200, b'{"valid":true}')
            current_bytes = contents()
            if self.headers.get('If-Match') != tag(current_bytes):
                return self.respond(409, b'{"error":"file changed"}')
            current = json.loads(current_bytes)
            if transition:
                data = copy.deepcopy(current)
                if self.path == '/api/submit':
                    if data['round']['status'] != 'draft':
                        raise ValueError('本轮已经提交')
                    data['history'].append({'number':data['round']['number'], 'submittedAt':datetime.now(timezone.utc).isoformat(), 'rows':copy.deepcopy(data['rows']), 'cameraParameters':camera_parameters()})
                    data['round']['status'] = 'submitted'
                else:
                    if data['round']['status'] != 'submitted':
                        raise ValueError('请先提交本轮')
                    data['round'] = {'number':data['round']['number']+1, 'status':'draft'}
                    for row in data['rows']:
                        row['status'], row['notes'] = ('暂不复测' if row['status'] in ['通过', '暂不复测'] else '待测'), ''
            else:
                if current['round']['status'] != 'draft':
                    raise ValueError('已提交的轮次不能修改，请开始下一轮')
                for key in ['round', 'history', 'legacySnapshot']:
                    if data.get(key) != current.get(key):
                        raise ValueError('历史与轮次不能覆盖')
                for old_row in current['rows']:
                    incoming = next((r for r in data.get('rows', []) if r.get('id') == old_row['id']), {})
                    if 'cases' in old_row and 'cases' not in incoming:
                        raise ValueError('请刷新新版页面后编辑，不能覆盖已有条件记录')
            validate(data)
            data['updatedAt'] = datetime.now(timezone.utc).isoformat()
            encoded = persist(data)
            self.respond(200, encoded if transition else b'{"saved":true}', etag=tag(encoded))
        except (ValueError, OSError) as error:
            self.respond(400, json.dumps({'error': str(error)}).encode('utf-8'))

if __name__ == '__main__':
    try:
        server = ReviewServer((HOST, PORT), Handler)
    except OSError:
        with urlopen(ORIGIN + '/health', timeout=2) as existing:
            health = json.load(existing)
            if health.get('editor') != 'puzzleapple-2026-10-24-01':
                raise RuntimeError('Local editor port is occupied')
            if health.get('revision') != SERVICE_REVISION:
                raise RuntimeError('旧版验收服务仍在运行，需要重启该服务。现有页面内容可保留，无需刷新。')
        if '--no-browser' not in sys.argv:
            webbrowser.open(ORIGIN)
        sys.exit(0)
    if '--no-browser' not in sys.argv:
        webbrowser.open(ORIGIN)
    server.serve_forever()
