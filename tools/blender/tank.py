"""Blockout MBT -> Assets/Art/Vehicles/Tank.fbx

Two roots so Unity can show one and hide the other:
  Tank_Intact  (Hull, Turret, Barrel, Track_L, Track_R)
  Tank_Wreck   (burnt hull + ajar turret)
Approx size: 9.8 m long, 3.6 m wide, 2.4 m tall.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def build():
    C.reset_scene()

    # ---- intact --------------------------------------------------------
    intact = []
    hull = C.add_wedge("Tank_Hull", (3.6, 7.2, 1.3), location=(0, 0, 1.0),
                       material="hull_green")
    C.bevel(hull, width=0.08, segments=1)
    intact.append(hull)

    for sx in (-1, 1):
        track = C.add_box(f"Tank_Track_{'L' if sx < 0 else 'R'}",
                          (0.7, 7.6, 0.9), location=(sx * 1.7, 0, 0.55),
                          material="tread_dark")
        intact.append(track)
        for i in range(6):
            wheel = C.add_cylinder(
                f"Tank_Wheel_{'L' if sx < 0 else 'R'}_{i}", 0.45, 0.3,
                location=(sx * 1.7, -3.0 + i * 1.2, 0.5),
                rotation=(0, math.radians(90), 0), material="metal_grey")
            intact.append(wheel)

    turret = C.add_wedge("Tank_Turret", (2.8, 3.4, 0.95), location=(0, 0.3, 2.0),
                         material="hull_green")
    C.bevel(turret, width=0.06, segments=1)
    intact.append(turret)

    mantlet = C.add_box("Tank_Mantlet", (0.9, 0.6, 0.7), location=(0, 2.0, 2.0),
                        material="metal_grey")
    intact.append(mantlet)
    barrel = C.add_cylinder("Tank_Barrel", 0.12, 4.6,
                            location=(0, 4.1, 2.05),
                            rotation=(math.radians(90), 0, 0), material="metal_grey")
    intact.append(barrel)

    cupola = C.add_cylinder("Tank_Cupola", 0.5, 0.4, location=(0.7, -0.4, 2.6),
                            material="hull_green")
    intact.append(cupola)

    intact_mesh = C.join(intact, "Tank_Intact")
    C.set_origin_to_base(intact_mesh)
    intact_mesh.location = (0, 0, 0)

    # ---- wreck --------------------------------------------------------
    wreck_parts = []
    wh = C.add_wedge("Tank_Wreck_Hull", (3.6, 7.2, 1.25), location=(0, 0, 1.0),
                     material="burnt")
    wreck_parts.append(wh)
    for sx in (-1, 1):
        wt = C.add_box(f"Tank_Wreck_Track_{'L' if sx < 0 else 'R'}",
                       (0.7, 7.6, 0.85), location=(sx * 1.7, 0, 0.5),
                       material="burnt")
        wreck_parts.append(wt)
    wtur = C.add_wedge("Tank_Wreck_Turret", (2.8, 3.4, 0.9),
                       location=(0.5, -0.4, 2.05),
                       material="burnt")
    wtur.rotation_euler = (math.radians(6), 0, math.radians(22))
    wreck_parts.append(wtur)
    wbar = C.add_cylinder("Tank_Wreck_Barrel", 0.12, 4.4,
                          location=(1.4, 2.6, 2.4),
                          rotation=(math.radians(70), 0, math.radians(30)),
                          material="burnt")
    wreck_parts.append(wbar)

    wreck_mesh = C.join(wreck_parts, "Tank_Wreck")
    C.set_origin_to_base(wreck_mesh)
    wreck_mesh.location = (0, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "Vehicles", "Tank.fbx")
    C.export_fbx(out, objects=[intact_mesh, wreck_mesh])


if __name__ == "__main__":
    build()
