"""Convert a downloaded .glb / .gltf into a Unity-ready .fbx, keeping node names.

    blender --background --factory-startup --python tools/blender/convert_gltf.py -- <in.glb> <out.fbx>

Used to bring CC0 / CC-BY model packs in without a Unity glTF import package.
"""
import os
import sys

import bpy


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if len(args) < 2:
        print("usage: convert_gltf.py -- <in.glb> <out.fbx>", file=sys.stderr)
        sys.exit(1)
    src, dst = os.path.abspath(args[0]), os.path.abspath(args[1])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)

    # glTF importer often nests everything under an empty; keep names, drop the
    # extra empty so Unity's hierarchy is flat.
    for obj in list(bpy.data.objects):
        if obj.type == "EMPTY" and not obj.children_recursive:
            bpy.data.objects.remove(obj, do_unlink=True)

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        global_scale=1.0,
        axis_forward="-Z",
        axis_up="Y",
        object_types={"MESH", "EMPTY", "ARMATURE"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )
    print("[ironfield] converted", src, "->", dst)


if __name__ == "__main__":
    main()
