"""Blockout IFV/APC -> Assets/Art/Vehicles/IFV.fbx
Roots: IFV_Intact, IFV_Wreck.  Approx 6.8 m long, 3.2 m wide, 2.8 m tall.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def build():
    C.reset_scene()

    intact = []
    hull = C.add_wedge("IFV_Hull", (3.0, 6.0, 1.9), location=(0, 0, 1.3),
                       material="hull_sand")
    C.bevel(hull, width=0.06, segments=1)
    intact.append(hull)

    for sx in (-1, 1):
        skirt = C.add_box(f"IFV_Skirt_{'L' if sx < 0 else 'R'}",
                          (0.35, 6.0, 1.0), location=(sx * 1.45, 0, 0.7),
                          material="hull_sand")
        intact.append(skirt)
        for i in range(4):
            w = C.add_cylinder(f"IFV_Wheel_{'L' if sx < 0 else 'R'}_{i}", 0.55, 0.4,
                               location=(sx * 1.5, -2.1 + i * 1.4, 0.55),
                               rotation=(0, math.radians(90), 0),
                               material="tread_dark")
            intact.append(w)

    tur = C.add_box("IFV_Turret", (1.4, 1.8, 0.8), location=(0, -0.3, 2.5),
                    material="hull_sand")
    C.bevel(tur, width=0.04, segments=1)
    intact.append(tur)
    cannon = C.add_cylinder("IFV_Cannon", 0.07, 2.6, location=(0, 1.4, 2.55),
                            rotation=(math.radians(90), 0, 0), material="metal_grey")
    intact.append(cannon)

    intact_mesh = C.join(intact, "IFV_Intact")
    C.set_origin_to_base(intact_mesh)
    intact_mesh.location = (0, 0, 0)

    wreck_parts = []
    wh = C.add_wedge("IFV_Wreck_Hull", (3.0, 6.0, 1.8), location=(0, 0, 1.25),
                     material="burnt")
    wreck_parts.append(wh)
    wtur = C.add_box("IFV_Wreck_Turret", (1.4, 1.8, 0.75), location=(0.6, -0.6, 2.4),
                     material="burnt")
    wtur.rotation_euler = (0, 0, math.radians(18))
    wreck_parts.append(wtur)
    for sx in (-1, 1):
        ws = C.add_box(f"IFV_Wreck_Skirt_{'L' if sx < 0 else 'R'}",
                       (0.35, 6.0, 0.95), location=(sx * 1.45, 0, 0.65),
                       material="burnt")
        wreck_parts.append(ws)

    wreck_mesh = C.join(wreck_parts, "IFV_Wreck")
    C.set_origin_to_base(wreck_mesh)
    wreck_mesh.location = (0, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "Vehicles", "IFV.fbx")
    C.export_fbx(out, objects=[intact_mesh, wreck_mesh])


if __name__ == "__main__":
    build()
