"""Modular village ruins -> Assets/Art/Ruins/RuinsKit.fbx

Separate objects, each origin at base centre, so Unity can scatter them:
  Ruin_WallLong, Ruin_WallCorner, Ruin_RubblePile, Ruin_HouseShell
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def wall_long():
    w = C.add_box("Ruin_WallLong", (6.0, 0.4, 3.2), location=(0, 0, 1.6),
                  material="concrete")
    # knock the top edge about a bit
    b = C.bevel
    b(w, width=0.05, segments=1)
    hole = C.add_box("_win", (1.2, 0.6, 1.2), location=(-1.5, 0, 2.0))
    _boolean_diff(w, hole)
    hole2 = C.add_box("_win2", (1.2, 0.6, 1.2), location=(1.6, 0, 1.6))
    _boolean_diff(w, hole2)
    C.set_origin_to_base(w)
    w.location = (0, 0, 0)
    return w


def wall_corner():
    a = C.add_box("Ruin_WallCorner", (4.0, 0.4, 2.6), location=(0, 0, 1.3),
                  material="concrete_dmg")
    bpart = C.add_box("_leg", (0.4, 3.4, 2.2), location=(-1.8, 1.7, 1.1),
                      material="concrete_dmg")
    merged = C.join([a, bpart], "Ruin_WallCorner")
    C.bevel(merged, width=0.04, segments=1)
    C.set_origin_to_base(merged)
    merged.location = (0, 0, 0)
    return merged


def rubble_pile():
    chunks = []
    for i in range(9):
        s = 0.5 + (i % 3) * 0.3
        c = C.add_box(f"_r{i}", (s, s * 1.2, s),
                      location=((i * 0.7) % 3 - 1.5,
                                (i * 1.3) % 3 - 1.5,
                                0.2 + (i % 4) * 0.25),
                      rotation=(math.radians(i * 15), math.radians(i * 22), 0),
                      material="concrete_dmg")
        chunks.append(c)
    pile = C.join(chunks, "Ruin_RubblePile")
    C.set_origin_to_base(pile)
    pile.location = (0, 0, 0)
    return pile


def house_shell():
    parts = []
    for sx, sy, sizex, sizey in ((0, 1, 7.0, 0.4), (0, -1, 7.0, 0.4),
                                 (1, 0, 0.4, 6.0), (-1, 0, 0.4, 6.0)):
        wseg = C.add_box(f"_hw_{sx}_{sy}", (sizex, sizey, 3.0),
                         location=(sx * 3.3, sy * 2.8, 1.5),
                         material="concrete")
        parts.append(wseg)
    # blow a gap in the front wall
    gap = C.add_box("_door", (2.0, 1.0, 2.2), location=(0, -2.8, 1.4))
    shell = C.join(parts, "Ruin_HouseShell")
    _boolean_diff(shell, gap)
    C.bevel(shell, width=0.04, segments=1)
    C.set_origin_to_base(shell)
    shell.location = (0, 0, 0)
    return shell


def _boolean_diff(target, cutter):
    m = target.modifiers.new(name="cut", type="BOOLEAN")
    m.operation = "DIFFERENCE"
    m.object = cutter
    import bpy
    bpy.context.view_layer.objects.active = target
    try:
        bpy.ops.object.modifier_apply(modifier=m.name)
    except RuntimeError:
        pass
    bpy.data.objects.remove(cutter, do_unlink=True)


def build():
    C.reset_scene()
    objs = []
    objs.append(wall_long()); objs[-1].location = (0, 0, 0)
    objs.append(wall_corner()); objs[-1].location = (14, 0, 0)
    objs.append(rubble_pile()); objs[-1].location = (26, 0, 0)
    objs.append(house_shell()); objs[-1].location = (38, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "Ruins", "RuinsKit.fbx")
    C.export_fbx(out, objects=objs)


if __name__ == "__main__":
    build()
