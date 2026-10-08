"""Editable apple-core source and FBX export. Run with Blender --background --python."""
import bpy, math, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.abspath(os.path.join(ROOT, '../../../..'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, roughness):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = roughness
    return m

skin = material('AppleCoreSkin', (.42, .014, .023), .32)
flesh = material('AppleCoreFlesh', (.83, .72, .48), .72)
stem = material('AppleCoreStem', (.105, .048, .018), .83)
nodes = flesh.node_tree.nodes
noise = nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 85
bump = nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = .16; bump.inputs['Distance'].default_value = .008
flesh.node_tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
flesh.node_tree.links.new(bump.outputs['Normal'], nodes.get('Principled BSDF').inputs['Normal'])

# Continuous, closed mesh: broad skin remnants at both ends, narrow uneven eaten middle.
profile = [(0,.012),(.012,.085),(.037,.159),(.067,.191),(.092,.178),(.120,.125),(.15,.085),(.21,.067),(.29,.074),(.36,.080),(.41,.117),(.445,.194),(.471,.216),(.498,.186),(.52,.105),(.531,.045),(.528,.008)]
N=96
verts=[]
for j,(z,r) in enumerate(profile):
    for i in range(N):
        a=2*math.pi*i/N
        middle=3<=j<=13
        # Different bite heights around the circumference, with small tooth scallops.
        wave=(.014*math.sin(5*a+.6)+.005*math.sin(13*a)) if middle else .002*math.sin(5*a)
        radial=1+.065*math.sin(5*a+.2)+(.06*math.sin(11*a+j*.5) if 5<=j<=11 else 0)
        rr=r*radial
        verts.append((rr*math.cos(a),rr*math.sin(a),z+wave))
faces=[]
for j in range(len(profile)-1):
    for i in range(N):
        k=(i+1)%N
        faces.append((j*N+i,j*N+k,(j+1)*N+k,(j+1)*N+i))
faces += [tuple(reversed(range(N))), tuple((len(profile)-1)*N+i for i in range(N))]
mesh=bpy.data.meshes.new('Eaten apple continuous surface');mesh.from_pydata(verts,[],faces);mesh.update()
obj=bpy.data.objects.new('Apple core',mesh);bpy.context.collection.objects.link(obj)
for m in [skin,flesh]:mesh.materials.append(m)
for p in mesh.polygons:
    ring=p.index//N
    # Skin wraps the outside of both remnants right up to the bite edge.
    # Only the inward-facing eaten surfaces expose flesh, not a peeled outer rim.
    p.material_index=0 if ring<=3 or ring>=11 else 1
    p.use_smooth=True
uv=mesh.uv_layers.new(name='UVMap')
for p in mesh.polygons:
    for li in p.loop_indices:
        v=mesh.vertices[mesh.loops[li].vertex_index].co
        uv.data[li].uv=((math.atan2(v.y,v.x)/(2*math.pi))%1,v.z/.535)
sub=obj.modifiers.new('Soft bite transitions','SUBSURF');sub.levels=2
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
bpy.ops.object.modifier_apply(modifier=sub.name)

# Short bent woody stalk; its root sits in the preserved top dimple.
curve=bpy.data.curves.new('Bent stem','CURVE');curve.dimensions='3D';curve.bevel_depth=.009;curve.bevel_resolution=3
sp=curve.splines.new('BEZIER');sp.bezier_points.add(3)
for p,co in zip(sp.bezier_points,[(0,0,.519),(.004,0,.551),(.018,.003,.586),(.022,.006,.61)]):
    p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
st=bpy.data.objects.new('Stem',curve);bpy.context.collection.objects.link(st);curve.materials.append(stem)
obj.select_set(False);st.select_set(True);bpy.context.view_layer.objects.active=st;bpy.ops.object.convert(target='MESH')
st=bpy.context.object

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True);st.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT,'AppleCore.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False)

world=bpy.data.worlds.new('Preview world');bpy.context.scene.world=world;world.use_nodes=True
world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.14,.17,1)
world.node_tree.nodes.get('Background').inputs[1].default_value=.35
def area(name,pos,power,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=pos
    o.rotation_euler=(Vector((0,0,.3))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large key',(-1,-1.5,2),110,1.6);area('Soft fill',(1,-.5,1),45,1.3);area('Rim',(.4,1,1.5),90,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.004));floor=bpy.context.object;floor.name='Preview floor';floor.data.materials.append(material('Preview neutral',(.085,.095,.11),.8))
camdata=bpy.data.cameras.new('Preview camera');cam=bpy.data.objects.new('Preview camera',camdata);bpy.context.collection.objects.link(cam)
cam.location=(.95,-1.65,.85);cam.rotation_euler=(Vector((0,0,.30))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=.84
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(PROJECT,'Temp','AppleCore-preview.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Source~','AppleCore.blend'))
bpy.ops.render.render(write_still=True)
