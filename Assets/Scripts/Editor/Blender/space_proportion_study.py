"""Run in Blender against the original corridor file; save a separate study."""
import bpy
from mathutils import Vector

output_blend = r'D:\NYU\Study\26Fall\Thesis\PuzzleAppleAssets\PuzzleApple-sacred-space-study.blend'
output_fbx = r'D:\NYU\Study\26Fall\Thesis\PuzzleApple\Assets\ArtAssets-3D\PuzzleApple-SpaceStudy.fbx'

def corridor_y(y):
    # Translate each wall inward, preserving its thickness.
    return y - .4 if y > .5 else y + .4 if y < -.5 else y

def corridor_z(z):
    return z - .57 if z > 2 else z

def room_y(y):
    return y + 1 if y > 3 else y - 1 if y < -3 else y

def room_z(z):
    return z + 4.03 if z > 7 else z

original_count = len(bpy.data.objects)
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH':
        continue
    inv = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        p = obj.matrix_world @ vertex.co
        if obj.name.startswith('走廊-') or obj.name == '门布尔':
            p.y = corridor_y(p.y)
            p.z = corridor_z(p.z)
        elif obj.name.startswith('主房-'):
            p.y = corridor_y(p.y) if obj.name == '主房-入口' and abs(p.y) < 2 else room_y(p.y)
            if obj.name == '主房-入口' and 2 < p.z < 3:
                p.z = corridor_z(p.z)
            else:
                p.z = room_z(p.z)
        elif obj.name.startswith('红地毯'):
            p.z += .01
        vertex.co = inv @ p
    obj.data.update()

# Preserve the user's newly reduced pedestal size; center it rather than moving it toward the rear.
pedestal = bpy.data.objects['立方体']
pedestal.location.x = bpy.data.objects['主房-顶底'].location.x
pedestal.location.y = 0
pedestal.location.z += .03

# Adjust existing lighting only. No new scene objects.
for obj in bpy.context.scene.objects:
    if obj.type == 'LIGHT' and obj.location.z > 5:
        obj.location.z += 4.03
        obj.location.x = pedestal.location.x
    elif obj.type == 'LIGHT':
        obj.location.z = min(obj.location.z, 1.7)
    elif obj.type == 'GREASEPENCIL':
        # The old drawing refers to the old proportions; keep it available but hidden.
        obj.hide_render = True
        obj.hide_set(True)

bpy.context.view_layer.update()
assert len(bpy.data.objects) == original_count
bpy.ops.wm.save_as_mainfile(filepath=output_blend)
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    if obj.type == 'MESH' and not obj.hide_render:
        obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=output_fbx, use_selection=True, object_types={'MESH'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                         use_mesh_modifiers=True, bake_anim=False, add_leaf_bones=False)
print('SPACE_STUDY_SAVED', output_blend, output_fbx, 'objects:', original_count)
