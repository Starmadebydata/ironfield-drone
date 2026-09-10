"""Blockout FPV / attack quad -> Assets/Art/Drone/Drone.fbx

Chunkier "attack quad" read: deep centre body + battery, tapered arms, fat
motors, 3-blade props (SEPARATE objects so Unity can spin them), an underslung
warhead, skids, a rear antenna and a forward camera pod.
Rough real-world size: ~0.55 m motor-to-motor.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def build():
    C.reset_scene()
    parts = []

    # --- centre body ------------------------------------------------------
    body = C.add_box("Drone_Body", (0.17, 0.24, 0.085), location=(0, 0, 0.135),
                     material="drone_carbon")
    C.bevel(body, width=0.016, segments=2)
    parts.append(body)

    battery = C.add_box("Drone_Battery", (0.11, 0.15, 0.05),
                        location=(0, -0.02, 0.185), material="drone_accent")
    C.bevel(battery, width=0.008, segments=1)
    parts.append(battery)

    canopy = C.add_wedge("Drone_Canopy", (0.13, 0.14, 0.07),
                         location=(0, 0.05, 0.175), material="drone_carbon")
    parts.append(canopy)

    # --- forward camera pod --------------------------------------------
    pod = C.add_wedge("Drone_CamPod", (0.07, 0.09, 0.06),
                      location=(0, 0.14, 0.15), material="drone_carbon")
    parts.append(pod)
    lens = C.add_cylinder("Drone_Lens", 0.024, 0.012,
                          location=(0, 0.19, 0.16),
                          rotation=(math.radians(90), 0, 0), material="glass_dark")
    parts.append(lens)

    # --- underslung warhead ------------------------------------------
    wh_body = C.add_cylinder("Drone_Warhead", 0.055, 0.16,
                             location=(0, 0.02, 0.055),
                             rotation=(math.radians(90), 0, 0), material="metal_grey")
    parts.append(wh_body)
    wh_tip = C.add_cylinder("Drone_WarheadTip", 0.055, 0.06,
                            location=(0, 0.13, 0.055),
                            rotation=(math.radians(90), 0, 0), verts=16,
                            material="drone_accent")
    parts.append(wh_tip)

    # --- arms + motors + props -------------------------------------
    motor_h = 0.05
    corners = {"FL": (-1, 1), "FR": (1, 1), "RL": (-1, -1), "RR": (1, -1)}
    for tag, (sx, sy) in corners.items():
        ang = math.atan2(sy, sx)
        # arm from body corner out to motor
        ax, ay = sx * 0.10, sy * 0.135
        mx, my = sx * 0.24, sy * 0.30
        arm = C.add_box(f"Drone_Arm_{tag}", (0.045, 0.30, 0.028),
                        location=((ax + mx) * 0.5, (ay + my) * 0.5, 0.125),
                        rotation=(0, 0, -ang + math.radians(90)),
                        material="drone_carbon")
        parts.append(arm)

        boom = C.add_cylinder(f"Drone_Boom_{tag}", 0.016, 0.20,
                              location=((ax + mx) * 0.5, (ay + my) * 0.5, 0.125),
                              rotation=(math.radians(90), 0, -ang + math.radians(90)),
                              material="metal_grey")
        parts.append(boom)

        motor = C.add_cylinder(f"Drone_Motor_{tag}", 0.026, motor_h,
                               location=(mx, my, 0.135), material="metal_grey")
        parts.append(motor)
        motor_top = C.add_cylinder(f"Drone_MotorBell_{tag}", 0.030, 0.02,
                                   location=(mx, my, 0.165), material="drone_accent")
        parts.append(motor_top)

        # 3-blade prop, kept SEPARATE (not joined) so Unity spins it
        hub = C.add_cylinder(f"Drone_Prop_{tag}", 0.02, 0.012,
                             location=(mx, my, 0.185), material="drone_carbon")
        blades = [hub]
        for b in range(3):
            ba = math.radians(b * 120)
            blade = C.add_box(f"_blade_{tag}_{b}", (0.19, 0.028, 0.005),
                              location=(mx, my, 0.185),
                              rotation=(0, 0, ba), material="drone_carbon")
            blades.append(blade)
        prop = C.join(blades, f"Drone_Prop_{tag}")
        C.set_origin_to_base(prop)
        prop.location = (mx, my, 0.185)

    # --- skids ---------------------------------------------------------
    for sx in (-1, 1):
        rail = C.add_box(f"Drone_Skid_{'L' if sx < 0 else 'R'}",
                         (0.02, 0.30, 0.018), location=(sx * 0.10, 0, 0.02),
                         material="drone_carbon")
        parts.append(rail)
        for sy in (-1, 1):
            strut = C.add_cylinder(
                f"Drone_Strut_{'L' if sx < 0 else 'R'}{'F' if sy > 0 else 'B'}",
                0.008, 0.09, location=(sx * 0.10, sy * 0.1, 0.07),
                material="drone_carbon")
            parts.append(strut)

    ant = C.add_cylinder("Drone_Antenna", 0.006, 0.16,
                         location=(0, -0.13, 0.22), material="metal_grey")
    parts.append(ant)

    hull = C.join(parts, "Drone_Body")
    C.set_origin_to_base(hull)
    hull.location = (0, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "Drone", "Drone.fbx")
    C.export_fbx(out)


if __name__ == "__main__":
    build()
