"""Blockout fixed-wing recon/strike drone -> Assets/Art/ReconDrone/ReconDrone.fbx

A twin-boom, high-wing MALE-style UAV silhouette (bulbous sensor nose, long
straight wing, twin tail booms to an inverted-V tail, rear pusher prop) —
loosely inspired by real-world fixed-wing military UAVs but deliberately
generic: no national markings, unit numbers, or faction insignia (matches
this project's fictional-setting rule). A visually distinct third playable
drone type alongside the quad-style Light/Heavy, read as "reconnaissance/
long-range strike" rather than "attack quad".

+Y = forward (nose), +Z = up, +X = right — same convention as drone.py.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import _common as C  # noqa: E402


def build():
    C.reset_scene()
    parts = []

    # --- central fuselage pod -------------------------------------------
    fuse = C.add_cylinder("Recon_Fuselage", 0.085, 0.95,
                          location=(0, 0.05, 0.16),
                          rotation=(math.radians(90), 0, 0),
                          verts=12, material="drone_carbon")
    parts.append(fuse)

    nose = C.add_cylinder("Recon_Nose", 0.05, 0.22,
                          location=(0, 0.62, 0.16),
                          rotation=(math.radians(90), 0, 0),
                          verts=12, material="drone_carbon")
    parts.append(nose)
    nose_tip = C.add_cylinder("Recon_NoseTip", 0.018, 0.06,
                              location=(0, 0.76, 0.16),
                              rotation=(math.radians(90), 0, 0),
                              verts=10, material="drone_accent")
    parts.append(nose_tip)

    # sensor turret slung under the nose (EO/IR ball, the recon drone's
    # signature "chin" — distinguishes the silhouette from the attack quad's
    # forward camera pod at a glance)
    sensor = C.add_cylinder("Recon_SensorBall", 0.05, 0.05,
                            location=(0, 0.58, 0.055),
                            rotation=(math.radians(90), 0, 0),
                            verts=14, material="glass_dark")
    parts.append(sensor)

    tail_taper = C.add_cylinder("Recon_TailTaper", 0.055, 0.16,
                                location=(0, -0.5, 0.16),
                                rotation=(math.radians(90), 0, 0),
                                verts=12, material="drone_carbon")
    parts.append(tail_taper)

    # --- high-mounted straight wing --------------------------------------
    span = 2.6
    wing = C.add_box("Recon_Wing", (span, 0.26, 0.035),
                     location=(0, 0.08, 0.32), material="drone_carbon")
    C.bevel(wing, width=0.012, segments=1)
    parts.append(wing)
    # slight tapered tips read better than a hard rectangular cut
    for sx in (-1, 1):
        tip = C.add_box(f"Recon_WingTip_{'L' if sx < 0 else 'R'}",
                        (0.14, 0.16, 0.028),
                        location=(sx * (span * 0.5 + 0.05), 0.03, 0.32),
                        material="drone_carbon")
        parts.append(tip)

    # --- twin tail booms + inverted-V tail --------------------------------
    boom_x = span * 0.36
    boom_len = 1.05
    for sx in (-1, 1):
        tag = "L" if sx < 0 else "R"
        boom = C.add_cylinder(f"Recon_Boom_{tag}", 0.028, boom_len,
                              location=(sx * boom_x, 0.08 - boom_len * 0.5, 0.27),
                              rotation=(math.radians(90), 0, 0),
                              verts=10, material="metal_grey")
        parts.append(boom)

        tail_y = 0.08 - boom_len
        # inverted-V fin: slopes outward/down from the boom's rear, the
        # signature MALE-UAV tail silhouette (distinct from a conventional
        # upright fin+rudder)
        fin = C.add_box(f"Recon_TailFin_{tag}", (0.02, 0.22, 0.34),
                        location=(sx * boom_x, tail_y, 0.24),
                        rotation=(0, 0, sx * math.radians(35)),
                        material="drone_carbon")
        parts.append(fin)

    # --- rear pusher prop --------------------------------------------------
    # Hub + 2 blades built with their THIN axis along local Y (forward) —
    # BuildDronePrefab derives each prop's spin axis from its own thinnest
    # mesh dimension, so a pusher prop (disc perpendicular to the fuselage's
    # long axis) spins correctly without any special-casing in the C# build
    # step or the runtime spin code.
    prop_y = -0.62
    hub = C.add_cylinder("Recon_Prop", 0.022, 0.014,
                         location=(0, prop_y, 0.16),
                         rotation=(math.radians(90), 0, 0),
                         verts=12, material="drone_carbon")
    blades = [hub]
    for b in range(3):
        ba = math.radians(b * 120)
        blade = C.add_box(f"_rblade_{b}", (0.34, 0.006, 0.038),
                          location=(0, prop_y, 0.16),
                          rotation=(0, ba, 0), material="drone_carbon")
        blades.append(blade)
    prop = C.join(blades, "Recon_Prop")
    C.set_origin_to_base(prop)
    prop.location = (0, prop_y, 0.16)

    hull = C.join(parts, "Recon_Body")
    C.set_origin_to_base(hull)
    hull.location = (0, 0, 0)

    out = os.path.join(C.repo_root(), "Assets", "Art", "ReconDrone", "ReconDrone.fbx")
    C.export_fbx(out)


if __name__ == "__main__":
    build()
