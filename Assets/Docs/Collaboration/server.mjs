import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomUUID, createHash} from 'node:crypto';

const here = path.dirname(fileURLToPath(import.meta.url));
const heading = '# 协作清单\n\n';
const statuses = ['待处理', '进行中', '待确认', '已完成'];
const fingerprint = raw => createHash('sha256').update(raw).digest('hex');
const quote = value => value.split('\n').map(line => '> ' + line).join('\n');
const unquote = block => block.split('\n').map(line => line.slice(2)).join('\n');

function formatEntry(item) {
  const state = item.archived ? ' · 已归档' : '';
  const ref = item.ref ? '\n\n参考\n' + quote(item.ref) : '';
  return '### #' + item.id + ' · ' + item.status + state + '\n\n' + quote(item.body) + ref;
}

function readBoard(raw) {
  const content = raw.replace(/\r\n/g, '\n').trimEnd();
  if (!content.startsWith(heading)) throw Error('清单文件缺少标题。');
  return content.slice(heading.length).split(/\n\n(?=## V\d+)/).map(block => {
    const [header, ...parts] = block.split('\n\n');
    const match = /^## V(\d+)(?: · (.+))?$/.exec(header);
    if (!match) throw Error('无法读取版本标题。');
    const version = Number(match[1]);
    const items = [];
    if (parts.join('\n\n') !== '（暂无条目）') {
      for (const entry of parts.join('\n\n').split(/\n\n(?=### #)/)) {
        const fields = /^### #(\d+) · (待处理|进行中|待确认|已完成)( · 已归档)?\n\n(> [^\n]*(?:\n> [^\n]*)*)(?:\n\n参考\n(> [^\n]*(?:\n> [^\n]*)*))?$/.exec(entry);
        if (!fields) throw Error('无法读取 V' + version + ' 的条目。');
        items.push({id:Number(fields[1]), status:fields[2], archived:!!fields[3],
          body:unquote(fields[4]), ref:fields[5] ? unquote(fields[5]) : ''});
      }
    }
    return {version, at:match[2] || '', items};
  });
}

export function createServer(file = path.join(here, '清单.md')) {
  return http.createServer(async (req, res) => {
    const host = '127.0.0.1:' + res.socket.localPort;
    const send = (code, data) => {
      res.writeHead(code, {'Content-Type':'application/json; charset=utf-8'});
      res.end(JSON.stringify(data));
    };
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Frame-Options', 'DENY');
    try {
      if (req.headers.host !== host) return send(403, '仅限本机访问。');
      if (req.method === 'GET' && req.url === '/') {
        res.writeHead(200, {'Content-Type':'text/html; charset=utf-8'});
        return res.end(fs.readFileSync(path.join(here, 'index.html')));
      }
      if (req.url !== '/board') return send(404, '找不到页面。');
      const state = raw => ({app:'PuzzleApple-Collaboration', revision:fingerprint(raw), versions:readBoard(raw)});
      if (req.method === 'GET') return send(200, state(fs.readFileSync(file, 'utf8')));
      if (req.method !== 'POST' || req.headers.origin !== 'http://' + host ||
          req.headers['content-type'] !== 'application/json') return send(403, '请从清单页面保存。');
      const chunks = [];
      for await (const chunk of req) chunks.push(chunk);
      const payload = JSON.parse(Buffer.concat(chunks).toString('utf8'));
      const raw = fs.readFileSync(file, 'utf8');
      const current = state(raw);
      if (payload.revision !== current.revision) return send(409, '已有新版本。请先复制未保存的修改，再刷新页面。');
      const latest = current.versions[0];
      const items = payload.items;
      if (!Array.isArray(items) || !items.every(i => i && Number.isSafeInteger(i.id) && i.id > 0 &&
          statuses.includes(i.status) && typeof i.archived === 'boolean' &&
          typeof i.body === 'string' && i.body.trim() && typeof i.ref === 'string') ||
          new Set(items.map(i => i.id)).size !== items.length ||
          latest.items.some(old => !items.some(i => i.id === old.id))) return send(400, '条目无效，请检查内容。');
      const entries = items.map(formatEntry).join('\n\n');
      if (entries === latest.items.map(formatEntry).join('\n\n')) return send(200, current);
      const next = heading + '## V' + (latest.version + 1) + ' · ' + new Date().toISOString() +
        '\n\n' + entries + '\n\n' + raw.replace(/\r\n/g, '\n').slice(heading.length);
      const result = state(next);
      const temp = file + '.' + randomUUID() + '.tmp';
      try {
        fs.writeFileSync(temp, next, {flag:'wx'});
        fs.renameSync(temp, file);
      } finally { fs.rmSync(temp, {force:true}); }
      send(200, result);
    } catch (error) { send(400, error.message); }
  });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const url = 'http://127.0.0.1:4318';
  const server = createServer();
  server.on('error', async error => {
    if (error.code === 'EADDRINUSE') {
      try {
        const board = await (await fetch(url + '/board')).json();
        if (board.app === 'PuzzleApple-Collaboration') return;
      } catch {}
    }
    console.error(error.message);
    process.exitCode = 1;
  });
  server.listen(4318, '127.0.0.1', () => console.log(url));
}
