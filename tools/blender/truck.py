"""Blockout 6x6 supply truck -> Assets/Art/Vehicles/Truck.fbx
Roots: Truck_Intact, Truck_Wreck.  Approx 8.5 m long, 2.6 m wide, 3.3 m tall.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def build():
    C.reset_scene()

    intact = []
    chassis = C.add_box("Truck_Chassis", (2.4, 8.0, 0.4), location=(0, 0, 1.0),
                        material="metal_grey")
    intact.append(chassis)

    cab = C.add_box("Truck_Cab", (2.4, 2.0, 1.8), location=(0, 2.9, 2.1),
                    material="hull_green")
    C.bevel(cab, width=0.05, segments=1)
    intact.append(cab)
    wind = C.add_box("Truck_Windshield", (2.0, 0.1, 1.0), location=(0, 1.95, 2.4),
                     material="glass_dark")
    intact.append(wind)

    bed = C.add_box("Truck_Bed", (2.5, 5.0, 1.9), location=(0, -1.4, 2.2),
                    material="hull_sand")
    intact.append(bed)
    canopy = C.add_box("Truck_Canopy", (2.55, 5.0, 0.15), location=(0, -1.4, 3.15),
                       material="hull_sand")
    intact.append(canopy)

    for sx in (-1, 1):
        for j, y in enumerate((2.7, -1.4, -2.7)):
            w = C.add_cylinder(f"Truck_Wheel_{'L' if sx < 0 else 'R'}_{j}", 0.6, 0.45,
                               location=(sx * 1.25, y, 0.6),
                               rotation=(0, math.radians(90), 0), material="tread_dark")
            intact.append(w)

    intact_mesh = C.join(intact, "Truck_Intact")
    C.set_origin_to_base(intact_mesh)
    intact_mesh.location = (0, 0, 0)

    wreck_parts = []
    wch = C.add_box("Truck_Wreck_Chassis", (2.4, 8.0, 0.4), location=(0, 0, 0.9),
                    material="burnt")
    wreck_parts.append(wch)
    wcab = C.add_box("Truck_Wreck_Cab", (2.4, 2.0, 1.6), location=(0.2, 2.9, 1.9),
                     material="burnt")
    wcab.rotation_euler = (math.radians(-8), 0, math.radians(10))
    wreck_parts.append(wcab)
    wbed = C.add_box("Truck_Wreck_Bed", (2.5, 4.6, 1.3), location=(0, -1.4, 1.8),
                     material="burnt")
    wreck_parts.append(wbed)

    wreck_mesh = C.join(wreck_parts, "Truck_Wreck")
    C.set_origin_to_base(wreck_mesh)
    wreck_mesh.location = (0, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "Vehicles", "Truck.fbx")
    C.export_fbx(out, objects=[intact_mesh, wreck_mesh])


if __name__ == "__main__":
    build()
