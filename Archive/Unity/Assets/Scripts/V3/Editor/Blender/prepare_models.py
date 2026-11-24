"""Extract V3 props from authored files without modifying external source files.
Run: blender --background --python <this file> -- <source-folder> <output-folder>
"""
import bpy
import sys
from pathlib import Path
from mathutils import Vector

source, output = [Path(p) for p in sys.argv[sys.argv.index('--') + 1:]]
output.mkdir(parents=True, exist_ok=True)

def load_fbx(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source / name))
    bpy.context.view_layer.update()

def export(objects, name, origin, scale=1.0):
    # Bake authored world transforms, then use a consistent bottom/center pivot.
    baked=[]
    for obj in objects:
        mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        matrix=obj.matrix_world.copy()
        for vertex in mesh.vertices:
            vertex.co=(matrix @ vertex.co - origin) * scale
        new=bpy.data.objects.new(obj.name + '_V3', mesh)
        bpy.context.collection.objects.link(new)
        baked.append(new)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in baked: obj.select_set(True)
    bpy.context.view_layer.objects.active=baked[0]
    bpy.ops.export_scene.fbx(filepath=str(output / name),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False)
    print('EXPORTED',name,[o.name for o in baked])

load_fbx('PuzzleApple-door.fbx')
door=[bpy.data.objects[n] for n in ['Cube','Cube.001']]
points=[o.matrix_world @ Vector(c) for o in door for c in o.bound_box]
origin=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
origin.z=min(p.z for p in points)
export(door,'CorridorDoor.fbx',origin)

load_fbx('PuzzleApple-light.fbx')
lamp=bpy.data.objects['Cylinder']
points=[lamp.matrix_world @ Vector(c) for c in lamp.bound_box]
origin=sum(points,Vector())/len(points)
export([lamp],'DoorIndicator.fbx',origin)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source / 'balance.glb'))
bpy.context.view_layer.update()
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
points=[o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
origin=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
origin.z=min(p.z for p in points)
height=max(p.z for p in points)-origin.z
export(objects,'Balance.fbx',origin,2.4/height)
