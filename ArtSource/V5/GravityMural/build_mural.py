"""Run in Blender. Models and orthographic relief bakes; no external art dependencies.

The original Blender scene is preserved. Only the new source scene is written.
RGBA normal atlas stores linear tangent normal in RGB and recess occlusion in A.
"""
import bpy, math, pathlib
import numpy as np
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Textures/V5/Resources/V5/Murals'
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.get('Gravity Mural Source')
if scene:
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for col in list(scene.collection.children):
        bpy.data.collections.remove(col)
else:
    scene = bpy.data.scenes.new('Gravity Mural Source')
collections = []
for name in ['01 Suspended cubes', '02 Grounded cubes']:
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    collections.append(col)

verts = [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]
faces = [(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
def box(name, pos, size, rotation, col):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    col.objects.link(ob)
    ob.location = pos
    ob.scale = Vector(size) * .5
    ob.rotation_euler = tuple(math.radians(a) for a in rotation)
    return ob

# Eight correspondences retain horizontal position, scale and rotation on descent.
specs = [(-2.35,.08,2.70,.38,(23,17,13)),(-1.67,-.12,1.25,.47,(38,-21,41)),
         (-.99,.15,2.10,.35,(-16,31,68)),(-.33,-.08,3.04,.44,(29,12,-24)),
         (.36,.12,1.15,.40,(-28,19,31)),(1.03,-.15,2.45,.50,(17,-33,9)),
         (1.72,.11,1.68,.34,(41,26,-37)),(2.35,-.04,2.95,.42,(-19,-24,58))]
panels=[]
for state,col in enumerate(collections):
    objects=[]
    for i,(x,y,z,size,rot) in enumerate(specs):
        ob=box(('Air' if state==0 else 'Ground')+' cube %02d'%(i+1),(x,y,z),(size,)*3,rot,col)
        if state:
            # Keep natural tilted attitudes, as the game's directed descent does.
            r=ob.rotation_euler.to_matrix()
            ob.location.z=-min((r @ (Vector(v)*size*.5)).z for v in verts)+.012
        ob.location.x += (-3.35 if state==0 else 3.35)
        objects.append(ob)
    floor=box('Ground line %d'%state,((-3.35 if state==0 else 3.35),0,-.045),(5.75,.7,.075),(0,0,0),col)
    objects.append(floor)
    panels.append(objects)
camera_data=bpy.data.cameras.new('Orthographic mural camera')
camera=bpy.data.objects.new('Orthographic mural camera',camera_data)
scene.collection.objects.link(camera)
camera.location=(0,-18,5.2)
camera.rotation_euler=(Vector((0,0,1.5))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO'; camera_data.ortho_scale=13
scene.camera=camera
light_data=bpy.data.lights.new('Source preview key','AREA')
light=bpy.data.objects.new('Source preview key',light_data);scene.collection.objects.link(light)
light.location=(-3,-6,8);light_data.energy=1500;light_data.shape='DISK';light_data.size=8
light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=2048;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Mural source world');scene.world.color=(.18,.18,.18)
scene.view_settings.view_transform='Standard'
scene.view_layers[0].update()

W,H=1280,816
ANGLE=math.radians(12)
UP=np.array([0,math.sin(ANGLE),math.cos(ANGLE)])
TOWARD=np.array([0,-math.cos(ANGLE),math.sin(ANGLE)])
def blur(a,sigma):
    radius=int(math.ceil(sigma*3)); xx=np.arange(-radius,radius+1)
    k=np.exp(-xx*xx/(2*sigma*sigma));k/=k.sum()
    for axis in (0,1):
        pads=[(0,0)]*2;pads[axis]=(radius,radius)
        padded=np.pad(a,pads,mode='edge'); result=np.zeros_like(a)
        for i,weight in enumerate(k):
            sl=[slice(None)]*2;sl[axis]=slice(i,i+a.shape[axis])
            result+=padded[tuple(sl)]*weight
        a=result
    return a
def save_image(name,a,folder=OUT):
    if a.ndim==2:a=np.stack([a,a,a,np.ones_like(a)],axis=-1)
    a=np.ascontiguousarray(np.clip(a,0,1),dtype=np.float32)
    image=bpy.data.images.new(name,width=a.shape[1],height=a.shape[0],alpha=True,float_buffer=False)
    image.colorspace_settings.name='Non-Color'
    image.pixels.foreach_set(a.ravel());image.filepath_raw=str(folder/(name+'.png'))
    image.file_format='PNG';image.save();bpy.data.images.remove(image)
def bake(objects,offset):
    depth=np.full((H,W),-1e6,dtype=np.float32)
    labels=np.zeros((H,W),dtype=np.int32); relief=np.zeros((H,W),dtype=np.float32)
    for oi,ob in enumerate(objects):
        # Explicit matrix avoids dependence on the active scene's evaluated depsgraph.
        from mathutils import Matrix
        matrix=Matrix.Translation(ob.location) @ ob.rotation_euler.to_matrix().to_4x4() @ Matrix.Diagonal((*ob.scale,1))
        points=np.array([tuple(matrix @ v.co) for v in ob.data.vertices]);points[:,0]-=offset
        px=(points[:,0]+3.1)/6.2*(W-1)
        py=(points@UP+.35)/3.9525*(H-1)
        pz=points@TOWARD
        for fi,face in enumerate(ob.data.polygons):
            ids=list(face.vertices)
            for tri in [(ids[0],ids[1],ids[2]),(ids[0],ids[2],ids[3])]:
                x=px[list(tri)];y=py[list(tri)];z=pz[list(tri)]
                x0=max(0,int(np.floor(min(x))));x1=min(W,int(np.ceil(max(x)))+1)
                y0=max(0,int(np.floor(min(y))));y1=min(H,int(np.ceil(max(y)))+1)
                if x0>=x1 or y0>=y1:continue
                den=(y[1]-y[2])*(x[0]-x[2])+(x[2]-x[1])*(y[0]-y[2])
                if abs(den)<1e-8:continue
                yy,xx=np.mgrid[y0:y1,x0:x1]
                a=((y[1]-y[2])*(xx-x[2])+(x[2]-x[1])*(yy-y[2]))/den
                b=((y[2]-y[0])*(xx-x[2])+(x[0]-x[2])*(yy-y[2]))/den
                c=1-a-b; d=a*z[0]+b*z[1]+c*z[2]
                update=(a>=0)&(b>=0)&(c>=0)&(d>depth[y0:y1,x0:x1])
                depth[y0:y1,x0:x1][update]=d[update]
                labels[y0:y1,x0:x1][update]=1+oi*6+fi
                # Flatten depth to a sunken relief, always behind the original wall.
                rel=-.52+(d-(pz.max()+pz.min())*.5)*.62
                relief[y0:y1,x0:x1][update]=rel[update]
    mask=labels>0
    edge=np.zeros((H,W),bool)
    for axis in (0,1):
        edge|=(labels!=np.roll(labels,1,axis))|(labels!=np.roll(labels,-1,axis))
    groove=blur(edge.astype(np.float32),1.25)*mask
    height=blur((relief-groove*.45)*mask,1.65)
    cavity=np.clip(blur(mask.astype(np.float32),1.0)*.22+groove*1.15,0,1)
    return height,cavity
def normal(height,cavity):
    dy,dx=np.gradient(height)
    n=np.stack([-dx*48,-dy*48,np.ones_like(height)],axis=-1)
    n/=np.linalg.norm(n,axis=-1,keepdims=True)
    return np.concatenate([n*.5+.5,cavity[...,None]],axis=-1)

bakes=[]
for objects,offset,name in zip(panels,[-3.35,3.35],['Suspended','Grounded']):
    height,cavity=bake(objects,offset)
    save_image(name+'Height',1+height)
    save_image(name+'Normal',normal(height,cavity))
    bakes.append((height,cavity))
# Arrow has exactly the same incised cross section as the two scenes.
AW=256; yy,xx=np.mgrid[0:H,0:AW];x=xx/AW;y=yy/H
def segment(ax,ay,bx,by):
    vx=(bx-ax)*AW;vy=(by-ay)*H;qx=xx-ax*AW;qy=yy-ay*H
    t=np.clip((qx*vx+qy*vy)/(vx*vx+vy*vy),0,1)
    return np.sqrt((qx-t*vx)**2+(qy-t*vy)**2)
dist=np.minimum(segment(.12,.49,.86,.49),np.minimum(segment(.63,.57,.86,.49),segment(.63,.41,.86,.49)))
arrow=-.50*np.clip((5-dist)/2,0,1);arrow=blur(arrow.astype(np.float32),1.1)
height=np.concatenate([bakes[0][0],arrow,bakes[1][0]],axis=1)
cavity=np.concatenate([bakes[0][1],np.clip(-arrow*1.15,0,1),bakes[1][1]],axis=1)
save_image('GravityMuralNormal',normal(height,cavity))
save_image('GravityMuralHeight',1+height)
# Neutral relief proof, generated from the same normal texture as the game.
n=normal(height,cavity)[...,:3]*2-1
shade=np.clip(1+(n[...,0]*-.42+n[...,1]*.64+n[...,2]*.64-.64)*.7-cavity*.42,.05,1)
save_image('ReliefProof',shade,pathlib.Path(__file__).parent)
scene['Bake notes']='Orthographic 12 degree elevation. Two sets of eight matching cubes; tilted attitudes retained on descent. RGB tangent normal + A cavity, linear.'
bpy.data.libraries.write(str(pathlib.Path(__file__).parent/'GravityMural.blend'),{scene},fake_user=True)
print('Created two source models and six baked textures at',OUT)
