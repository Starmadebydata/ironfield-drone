"""Shared helpers for the Ironfield blockout model scripts.

Every model script imports this, builds clean separately-named objects at
real-world metric scale (Blender units = metres), then calls ``export_fbx`` to
write a Unity-ready FBX. Re-running a script fully regenerates its blockout, so
hand-detailing is expected to happen on the exported FBX / in a saved .blend,
not by editing these scripts.

Usage (headless):
    blender --background --factory-startup --python tools/blender/tank.py
"""

import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector


# --------------------------------------------------------------------------- #
# Scene lifecycle
# --------------------------------------------------------------------------- #
def reset_scene():
    """Wipe the default scene to a clean slate."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Metric, 1 unit = 1 m.
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def repo_root():
    # tools/blender/_common.py -> repo root is two levels up.
    return os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def art_dir(*parts):
    d = os.path.join(repo_root(), "Assets", "Art", *parts)
    os.makedirs(os.path.dirname(d) if os.path.splitext(d)[1] else d, exist_ok=True)
    return d


# --------------------------------------------------------------------------- #
# Mesh construction
# --------------------------------------------------------------------------- #
def add_box(name, size, location=(0, 0, 0), rotation=(0, 0, 0), material=None):
    """Axis-aligned box. ``size`` is full extents (x, y, z) in metres."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0] / 2.0, size[1] / 2.0, size[2] / 2.0)
    _apply_transform(obj, scale=True)
    if material:
        assign_material(obj, material)
    return obj


def add_cylinder(name, radius, depth, location=(0, 0, 0), rotation=(0, 0, 0),
                 verts=24, material=None):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=depth, vertices=verts,
        location=location, rotation=rotation,
    )
    obj = bpy.context.active_object
    obj.name = name
    if material:
        assign_material(obj, material)
    return obj


def add_wedge(name, size, location=(0, 0, 0), material=None):
    """A sloped box (front face lower than back) - handy for glacis / hulls."""
    x, y, z = size[0] / 2.0, size[1] / 2.0, size[2] / 2.0
    verts = [
        (-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z),   # bottom
        (-x, -y, z * 0.2), (x, -y, z * 0.2),                   # low front top
        (x, y, z), (-x, y, z),                                 # high back top
    ]
    faces = [
        (0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
        (2, 3, 7, 6), (1, 2, 6, 5), (0, 4, 7, 3),
    ]
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    if material:
        assign_material(obj, material)
    return obj


def bevel(obj, width=0.03, segments=2):
    mod = obj.modifiers.new(name="Bevel", type="BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(40)
    _apply_modifier(obj, mod.name)


def join(objects, name):
    """Join a list of objects into the first one and rename it."""
    objects = [o for o in objects if o is not None]
    if not objects:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def set_origin_to_base(obj):
    """Origin at the centre of the object's footprint, on its lowest point."""
    _apply_transform(obj, location=False, rotation=True, scale=True)
    bpy.context.view_layer.objects.active = obj
    bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    min_z = min(v.z for v in bbox)
    cx = sum(v.x for v in bbox) / 8.0
    cy = sum(v.y for v in bbox) / 8.0
    scene = bpy.context.scene
    cursor_prev = tuple(scene.cursor.location)
    scene.cursor.location = (cx, cy, min_z)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    scene.cursor.location = cursor_prev


# --------------------------------------------------------------------------- #
# Materials  (URP will rebuild these on import; colours are just a guide)
# --------------------------------------------------------------------------- #
_MATCACHE = {}

PALETTE = {
    "hull_green":   (0.16, 0.19, 0.12, 1.0),
    "hull_sand":    (0.45, 0.40, 0.28, 1.0),
    "tread_dark":   (0.05, 0.05, 0.05, 1.0),
    "metal_grey":   (0.30, 0.31, 0.33, 1.0),
    "glass_dark":   (0.02, 0.03, 0.04, 1.0),
    "drone_carbon": (0.04, 0.04, 0.05, 1.0),
    "drone_accent": (0.75, 0.20, 0.10, 1.0),
    "burnt":        (0.03, 0.03, 0.03, 1.0),
    "concrete":     (0.55, 0.54, 0.50, 1.0),
    "concrete_dmg": (0.42, 0.40, 0.36, 1.0),
    "rebar":        (0.25, 0.15, 0.10, 1.0),
}


def get_material(key):
    if key in _MATCACHE:
        return _MATCACHE[key]
    mat = bpy.data.materials.new(name=key)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    rgba = PALETTE.get(key, (0.5, 0.5, 0.5, 1.0))
    bsdf.inputs["Base Color"].default_value = rgba
    if key in ("metal_grey", "tread_dark"):
        bsdf.inputs["Metallic"].default_value = 0.9
        bsdf.inputs["Roughness"].default_value = 0.5
    elif key == "glass_dark":
        bsdf.inputs["Roughness"].default_value = 0.05
        bsdf.inputs["Metallic"].default_value = 0.0
    else:
        bsdf.inputs["Roughness"].default_value = 0.8
    _MATCACHE[key] = mat
    return mat


def assign_material(obj, key):
    mat = get_material(key)
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# --------------------------------------------------------------------------- #
# Export
# --------------------------------------------------------------------------- #
def export_fbx(path, objects=None):
    """Write a Unity-facing FBX: +Y up, +Z forward, metres, applied transforms."""
    bpy.ops.object.select_all(action="DESELECT")
    export_objs = objects if objects is not None else list(bpy.context.scene.objects)
    for o in export_objs:
        if o.type in {"MESH", "EMPTY"}:
            o.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        global_scale=1.0,
        axis_forward="-Z",
        axis_up="Y",
        object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=False,
    )
    print("[ironfield] wrote", path)


# --------------------------------------------------------------------------- #
# internals
# --------------------------------------------------------------------------- #
def _apply_transform(obj, location=False, rotation=False, scale=False):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(
        location=location, rotation=rotation, scale=scale
    )


def _apply_modifier(obj, name):
    bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.object.modifier_apply(modifier=name)
    except RuntimeError as exc:  # headless edge cases
        print("[ironfield] modifier_apply skipped:", exc, file=sys.stderr)
