"""Blockout FPV / attack quad -> Assets/Art/Drone/Drone.fbx

Objects (kept separate so Unity can spin the props and mount a camera):
  Drone_Body, Drone_Arm_[FL/FR/RL/RR], Drone_Prop_[FL/FR/RL/RR], Drone_CamPod
Rough real-world size: ~0.35 m motor-to-motor.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def build():
    C.reset_scene()
    parts = []

    body = C.add_box("Drone_Body", (0.12, 0.16, 0.05), location=(0, 0, 0.10),
                     material="drone_carbon")
    C.bevel(body, width=0.012, segments=2)
    parts.append(body)

    stack = C.add_box("Drone_Stack", (0.05, 0.05, 0.03), location=(0, 0, 0.14),
                      material="drone_accent")
    parts.append(stack)

    pod = C.add_wedge("Drone_CamPod", (0.05, 0.06, 0.05), location=(0, 0.09, 0.12),
                      material="drone_carbon")
    parts.append(pod)
    lens = C.add_cylinder("Drone_Lens", 0.018, 0.01, location=(0, 0.13, 0.13),
                          rotation=(math.radians(90), 0, 0), material="glass_dark")
    parts.append(lens)

    arm_len = 0.14
    motor_h = 0.035
    corners = {
        "FL": (-1, 1), "FR": (1, 1), "RL": (-1, -1), "RR": (1, -1),
    }
    for tag, (sx, sy) in corners.items():
        ang = math.atan2(sy, sx)
        ax = sx * 0.085
        ay = sy * 0.11
        arm = C.add_box(f"Drone_Arm_{tag}", (0.022, arm_len, 0.015),
                        location=(ax * 0.5, ay * 0.5, 0.10),
                        rotation=(0, 0, -ang + math.radians(90)),
                        material="drone_carbon")
        parts.append(arm)

        mx = sx * 0.16
        my = sy * 0.20
        motor = C.add_cylinder(f"Drone_Motor_{tag}", 0.017, motor_h,
                               location=(mx, my, 0.11), material="metal_grey")
        parts.append(motor)

        prop = C.add_box(f"Drone_Prop_{tag}", (0.13, 0.014, 0.004),
                         location=(mx, my, 0.135), material="drone_carbon")
        # props stay SEPARATE objects (not joined) so Unity can spin them
        C.set_origin_to_base(prop)
        prop.location = (mx, my, 0.135)

    legs = []
    for sx in (-1, 1):
        leg = C.add_box(f"Drone_Skid_{'L' if sx < 0 else 'R'}",
                        (0.012, 0.18, 0.012), location=(sx * 0.07, 0, 0.03),
                        material="drone_carbon")
        legs.append(leg)
    parts += legs

    hull = C.join(parts, "Drone_Body")
    C.set_origin_to_base(hull)
    hull.location = (0, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "Drone", "Drone.fbx")
    C.export_fbx(out)


if __name__ == "__main__":
    build()
