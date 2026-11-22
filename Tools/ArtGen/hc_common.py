"""Shared Blender helpers for Hollowcrawl's pre-rendered art (run with the `bpy` module, Blender 5.x).

Every model is built procedurally in code so the whole art set can be regenerated and restyled
consistently. Renders are orthographic with a transparent background, Cycles on CPU.
"""
import math
import os
import random

import bpy
from mathutils import Vector  # noqa: F401  (re-exported for generators)

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPRITES = os.path.join(REPO, "Assets", "Resources", "Sprites")
TILES = os.path.join(REPO, "Assets", "Resources", "Tiles")

# --------------------------------------------------------------------------- scene


def reset(samples=24):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    world = bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.05, 0.05, 0.07, 1)
    bg.inputs[1].default_value = 0.35
    return sc


def camera(top_down=True, ortho_scale=1.0, tilt_deg=35.0):
    """top_down: straight down (tiles). Otherwise a high 3/4 view (actors) looking north."""
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    if top_down:
        cam.location = (0, 0, 10)
        cam.rotation_euler = (0, 0, 0)
    else:
        t = math.radians(tilt_deg)
        cam.location = (0, -10 * math.sin(t), 10 * math.cos(t))
        cam.rotation_euler = (t, 0, 0)
    bpy.context.scene.camera = cam
    return cam


def lights(key=1.7, warm=(1.0, 0.92, 0.8), rim_color=(0.6, 0.75, 1.0), rim=2.0):
    def sun(name, rot, energy, color):
        d = bpy.data.lights.new(name, "SUN")
        d.energy = energy
        d.color = color
        d.angle = math.radians(8)
        o = bpy.data.objects.new(name, d)
        o.rotation_euler = [math.radians(a) for a in rot]
        bpy.context.collection.objects.link(o)
    sun("Key", (35, 0, -35), key, warm)
    sun("Fill", (60, 0, 140), key * 0.25, (0.8, 0.85, 1.0))
    sun("Rim", (-110, 0, 0), rim, rim_color)


def render(path, size):
    sc = bpy.context.scene
    sc.render.resolution_x = size
    sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


# --------------------------------------------------------------------------- materials


def mat(name, color, rough=0.6, metal=0.0, emit=None, emit_strength=0.0, noise=0.15, bump=0.25, scale=8.0, sss=0.0):
    """Principled material with procedural colour variation and bump so surfaces never look flat."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if sss > 0:
        p.inputs["Subsurface Weight"].default_value = sss
    tex = nt.nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = scale
    tex.inputs["Detail"].default_value = 6.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    c = list(color) + [1] if len(color) == 3 else list(color)
    dark = [max(0, v * (1 - noise)) for v in c[:3]] + [1]
    light = [min(1, v * (1 + noise)) for v in c[:3]] + [1]
    ramp.color_ramp.elements[0].color = dark
    ramp.color_ramp.elements[1].color = light
    nt.links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], p.inputs["Base Color"])
    bmp = nt.nodes.new("ShaderNodeBump")
    bmp.inputs["Strength"].default_value = bump
    nt.links.new(tex.outputs["Fac"], bmp.inputs["Height"])
    nt.links.new(bmp.outputs["Normal"], p.inputs["Normal"])
    if emit is not None:
        p.inputs["Emission Color"].default_value = list(emit) + [1]
        p.inputs["Emission Strength"].default_value = emit_strength
    return m


def stone_mat(name, color, mortar=(0.08, 0.07, 0.07), scale=3.0, bump=0.6):
    """Flagstone / brick look: voronoi cells with dark mortar lines."""
    m = mat(name, color, rough=0.85, noise=0.25, bump=0.3, scale=14)
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    base_link = p.inputs["Base Color"].links[0]
    base_out = base_link.from_socket
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.feature = "DISTANCE_TO_EDGE"
    vor.inputs["Scale"].default_value = scale
    edge = nt.nodes.new("ShaderNodeMapRange")
    edge.inputs["From Min"].default_value = 0.0
    edge.inputs["From Max"].default_value = 0.06
    nt.links.new(vor.outputs["Distance"], edge.inputs["Value"])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["B"].default_value = list(mortar) + [1]
    nt.links.new(edge.outputs["Result"], mix.inputs["Factor"])
    # factor 0 -> mortar (B is used at factor 1, so swap: A = mortar, B = stone)
    mix.inputs["A"].default_value = list(mortar) + [1]
    nt.links.new(base_out, mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], p.inputs["Base Color"])
    bmp = nt.nodes.new("ShaderNodeBump")
    bmp.inputs["Strength"].default_value = bump
    nt.links.new(edge.outputs["Result"], bmp.inputs["Height"])
    old = p.inputs["Normal"].links[0].from_node
    nt.links.new(old.outputs["Normal"], bmp.inputs["Normal"])
    nt.links.new(bmp.outputs["Normal"], p.inputs["Normal"])
    return m


def glow_mat(name, color, strength=6.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs[0].default_value = list(color) + [1]
    e.inputs[1].default_value = strength
    nt.links.new(e.outputs[0], nt.nodes["Material Output"].inputs[0])
    return m


# --------------------------------------------------------------------------- geometry


def _finish(obj, material=None, bevel=0.0, subsurf=0, smooth=True):
    if material is not None:
        obj.data.materials.append(material)
    if bevel > 0:
        b = obj.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = 3
    if subsurf:
        s = obj.modifiers.new("Sub", "SUBSURF")
        s.levels = subsurf
        s.render_levels = subsurf
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return obj


def sphere(loc, r, material=None, scale=(1, 1, 1), subsurf=1, segments=24):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=segments, ring_count=segments // 2)
    o = bpy.context.active_object
    o.scale = scale
    return _finish(o, material, subsurf=subsurf)


def ico(loc, r, material=None, scale=(1, 1, 1), subdiv=3, displace=0.0, seed=0, smooth=True):
    bpy.ops.mesh.primitive_ico_sphere_add(radius=r, location=loc, subdivisions=subdiv)
    o = bpy.context.active_object
    o.scale = scale
    if displace:
        t = bpy.data.textures.new("disp%d" % seed, "VORONOI")
        t.noise_scale = 0.35 * r / 0.4
        d = o.modifiers.new("Disp", "DISPLACE")
        d.texture = t
        d.strength = displace
    return _finish(o, material, smooth=smooth)


def box(loc, size, material=None, bevel=0.02, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return _finish(o, material, bevel=bevel, smooth=False)


def cyl(loc, r, depth, material=None, verts=32, bevel=0.0, rot=(0, 0, 0), r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, location=loc, vertices=verts, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(radius1=r, radius2=r2, depth=depth, location=loc, vertices=verts, rotation=rot)
    o = bpy.context.active_object
    return _finish(o, material, bevel=bevel, smooth=True)


def cone(loc, r, depth, material=None, verts=24, rot=(0, 0, 0), r2=0.0):
    return cyl(loc, r, depth, material, verts=verts, rot=rot, r2=r2)


def torus(loc, R, r, material=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=48, minor_segments=16)
    o = bpy.context.active_object
    return _finish(o, material)


def plane(loc, size, material=None):
    bpy.ops.mesh.primitive_plane_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = (size[0], size[1], 1)
    return _finish(o, material, smooth=False)


def rnd(seed):
    return random.Random(seed)


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexcol(h):
    """sRGB hex colour -> linear RGB tuple (what Blender material inputs expect)."""
    h = h.lstrip("#")
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4))
