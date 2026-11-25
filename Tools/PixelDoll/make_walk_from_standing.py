"""Generate an 8-frame walk cycle for a PixelDoll character from its standing
sprites, using the leg keypoints in manifest.json. Pure Python (no PIL).

Covered directions (8 frames, sin/cos phase, 1 px body dip near contact):
  south, north          feet pulled toward the centre line while walking,
                        knees bend alternately
  south-east, north-east (and south-west, north-west mirrored)
                        natural stance kept; legs swing in opposite phase
                        along the walking axis, front leg lower/higher
  east (and west mirrored)
                        legs overlap in the standing art, so they are
                        REDRAWN: tapered thigh/shin/foot capsules shaded with
                        the sprite's own skin ramp and outline, thigh +/-22
                        deg, knee bend up to 40 deg, body drops so the
                        stance foot stays on the ground.
A hand-drawn walk_east can replace the generated one; make west from it with
--mirror.

These are stand-ins for real walk art: legs are transformed from the still
pose, so they look stiffer than hand-drawn or PixelDoll-generated frames.

usage (from the repository root):
  generate:
    python Tools/PixelDoll/make_walk_from_standing.py ^
        KnightClient/Assets/_Project/Resources/Characters/Hero ^
        <pixeldoll-export>/manifest.json ^
        KnightClient/Assets/_Project/Resources/Characters/Hero [--preview out.png]
  mirror a hand-drawn east strip to west (each cell flipped, order kept):
    python Tools/PixelDoll/make_walk_from_standing.py --mirror ^
        KnightClient/Assets/_Project/Resources/Characters/Hero/walk_east_strip8.png ^
        KnightClient/Assets/_Project/Resources/Characters/Hero/walk_west_strip8.png
"""
import json
import math
import os
import struct
import sys
import zlib

CELL = 128
FRAMES = 8

# ---------------------------------------------------------------- PNG I/O
def read_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    pos, idat, width = 8, b'', None
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b'IHDR':
            width, height, depth, color, _, _, interlace = struct.unpack('>IIBBBBB', body)
            assert depth == 8 and color in (2, 6) and interlace == 0, (path, depth, color)
            channels = 4 if color == 6 else 3
        elif kind == b'IDAT':
            idat += body
        pos += 12 + length
    raw = zlib.decompress(idat)
    stride = width * channels
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(height):
        f = raw[i]; line = bytearray(raw[i + 1:i + 1 + stride]); i += 1 + stride
        for x in range(stride):
            a = line[x - channels] if x >= channels else 0
            b = prev[x]
            c = prev[x - channels] if x >= channels else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line); prev = line
    pixels = [[(0, 0, 0, 0)] * width for _ in range(height)]
    for y, line in enumerate(rows):
        for x in range(width):
            o = x * channels
            pixels[y][x] = tuple(line[o:o + 4]) if channels == 4 else tuple(line[o:o + 3]) + (255,)
    return pixels


def write_png(path, pixels):
    height, width = len(pixels), len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for p in row:
            raw.extend(p)
    def chunk(kind, body):
        return struct.pack('>I', len(body)) + kind + body + struct.pack('>I', zlib.crc32(kind + body) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(png)

# ---------------------------------------------------------------- helpers
EMPTY = (0, 0, 0, 0)

def blank():
    return [[EMPTY] * CELL for _ in range(CELL)]

def copy(img):
    return [row[:] for row in img]

def over(dst, x, y, p):
    if 0 <= x < CELL and 0 <= y < CELL and p[3] > 0:
        dst[y][x] = p

def flip(img):
    return [row[::-1] for row in img]

def shift(img, dy):
    out = blank()
    for y in range(CELL):
        for x in range(CELL):
            if img[y][x][3]:
                over(out, x, y + dy, img[y][x])
    return out

# ---------------------------------------------------------------- poses
# Smooth cycle: phase angle a = 2*pi*k/FRAMES. sin(a) drives the stride
# (which leg is forward and by how much), the swinging leg is lifted with
# max(0, +/-cos(a)) so it rises while passing and lands at full stride, and
# the body dips 1 px around each contact.

def split(img, top):
    """Body (rows above `top`) and legs (rows from `top`)."""
    body, legs = blank(), blank()
    for y in range(CELL):
        for x in range(CELL):
            (legs if y >= top else body)[y][x] = img[y][x]
    return body, legs

def outline_color(img):
    """Most common dark opaque colour = the sprite's line colour."""
    counts = {}
    for row in img:
        for p in row:
            if p[3] > 200 and sum(p[:3]) < 200:
                counts[p] = counts.get(p, 0) + 1
    return max(counts, key=counts.get) if counts else (30, 20, 20, 255)

def split_legs(legs, top, split_x, line=None):
    """Cut the leg region at split_x. With `line`, the cut edge of each half is
    redrawn as a 1 px outline so each leg keeps a closed contour."""
    left, right = blank(), blank()
    for y in range(top, CELL):
        for x in range(CELL):
            if legs[y][x][3]:
                (left if x < split_x else right)[y][x] = legs[y][x]
    if line is not None:
        for y in range(top, CELL):
            a, b = split_x - 1, split_x
            if 0 <= a < CELL and legs[y][a][3] and legs[y][b][3]:
                left[y][a] = line
                right[y][b] = line
    return left, right

def sheared(legs, top, foot, amount, lift=0.0):
    """Shear legs horizontally: 0 at `top`, `amount` px at the feet; lift raises the foot."""
    out = blank()
    span = max(1, foot - top)
    last_dest = None
    for y in range(top, CELL):
        t = min(1.0, (y - top) / span)
        dx = round(amount * t)
        dy = -round(lift * t)
        dest = y + dy
        # lowering stretches the leg: repeat this row into any skipped rows
        rows = [dest] if last_dest is None else list(range(last_dest + 1, dest + 1)) or [dest]
        last_dest = dest
        for x in range(CELL):
            if legs[y][x][3]:
                for row in rows:
                    over(out, x + dx, row, legs[y][x])
    return out

def knee_lifted(leg, knee, lift):
    """Front/back view: raise shin and foot (rows below the knee)."""
    out = blank()
    dy = round(lift)
    for y in range(CELL):
        for x in range(CELL):
            if leg[y][x][3]:
                over(out, x, y - (dy if y >= knee else 0), leg[y][x])
    return out

def compose(layers, body, bob):
    frame = blank()
    for layer in layers + [body]:
        for y in range(CELL):
            for x in range(CELL):
                over(frame, x, y, layer[y][x])
    return shift(frame, bob) if bob else frame

def phases():
    for k in range(FRAMES):
        a = 2 * math.pi * k / FRAMES
        yield math.sin(a), math.cos(a)

def body_bob(s):
    """1 px dip near full stride (contact)."""
    return 1 if abs(s) > 0.7 else 0

def side_frames(img, top, foot, split_x, stride, lift):
    """Side view: split the silhouette into the two legs (no duplication) and
    swing them in opposite phase; they pass each other like a real stride.
    The near leg (larger x side when facing east) is drawn on top."""
    body, legs = split(img, top)
    far, near = split_legs(legs, top, split_x, outline_color(img))
    frames = []
    for s, c in phases():
        far_leg = sheared(far, top, foot, -stride * s, lift * max(0.0, -c))
        near_leg = sheared(near, top, foot, stride * s, lift * max(0.0, c))
        frames.append(compose([far_leg, near_leg], body, body_bob(s)))
    return frames

def diagonal_frames(img, top, foot, split_x, swing, depth, lift, axis_y):
    """3/4 view: keep the natural stance (no extra spread). Legs swing in
    opposite phase along the walking axis: the forward foot moves toward the
    walking side (+x) and toward/away from the viewer (axis_y = +1 when
    walking south-east, -1 north-east)."""
    body, legs = split(img, top)
    left, right = split_legs(legs, top, split_x, outline_color(img))
    frames = []
    for s, c in phases():
        # positive s: left leg forward
        left_leg = sheared(left, top, foot, swing * s,
                           -axis_y * depth * s + lift * max(0.0, c))
        right_leg = sheared(right, top, foot, -swing * s,
                            axis_y * depth * s + lift * max(0.0, -c))
        frames.append(compose([left_leg, right_leg], body, body_bob(s)))
    return frames

def leg_palette(img, top):
    """Skin ramp + outline sampled from the leg area of the source sprite."""
    counts = {}
    for y in range(top, CELL):
        for x in range(CELL):
            p = img[y][x]
            if p[3] > 200 and sum(p[:3]) > 380:
                counts[p] = counts.get(p, 0) + 1
    skin = sorted(sorted(counts, key=counts.get, reverse=True)[:6],
                  key=lambda p: sum(p[:3]), reverse=True)
    light = skin[0]
    mid = skin[min(2, len(skin) - 1)]
    shadow = skin[min(4, len(skin) - 1)]
    deep = tuple(max(0, round(v * 0.82)) for v in shadow[:3]) + (255,)
    return {'light': light, 'mid': mid, 'shadow': shadow, 'deep': deep,
            'line': outline_color(img)}

def draw_segment(layer, ax, ay, bx, by, r0, r1, tone):
    """Tapered capsule from A (radius r0) to B (radius r1) with 1 px outline
    and 3-tone shading: light on the +x (facing) side, shadow on the back."""
    vx, vy = bx - ax, by - ay
    length2 = vx * vx + vy * vy or 1.0
    length = math.sqrt(length2)
    nx, ny = vy / length, -vx / length          # normal; flip so it points to +x
    if nx < 0:
        nx, ny = -nx, -ny
    x0, x1 = int(min(ax, bx) - r0 - 2), int(max(ax, bx) + r0 + 2)
    y0, y1 = int(min(ay, by) - r0 - 2), int(max(ay, by) + r0 + 2)
    for y in range(max(0, y0), min(CELL, y1 + 1)):
        for x in range(max(0, x0), min(CELL, x1 + 1)):
            px, py = x + 0.5, y + 0.5
            t = max(0.0, min(1.0, ((px - ax) * vx + (py - ay) * vy) / length2))
            cx, cy = ax + vx * t, ay + vy * t
            dist = math.hypot(px - cx, py - cy)
            r = r0 + (r1 - r0) * t
            if dist <= r:
                side = ((px - cx) * nx + (py - cy) * ny) / max(r, 0.1)
                colour = tone['light'] if side > 0.35 else tone['shadow'] if side < -0.4 else tone['mid']
                layer[y][x] = colour
            elif dist <= r + 1.0 and layer[y][x][3] == 0:
                layer[y][x] = tone['line']

def drawn_leg(hip, thigh_len, shin_len, thigh_angle, knee_bend, tone):
    """One leg facing east; angles in radians, 0 = straight down, + = forward."""
    hx, hy = hip
    kx = hx + thigh_len * math.sin(thigh_angle)
    ky = hy + thigh_len * math.cos(thigh_angle)
    shin_angle = thigh_angle - knee_bend
    ax = kx + shin_len * math.sin(shin_angle)
    ay = ky + shin_len * math.cos(shin_angle)
    layer = blank()
    # foot first so the shin outline sits over it
    toe_x, toe_y = ax + 6.5, ay + 2.0 + 1.5 * math.sin(knee_bend)
    draw_segment(layer, ax - 1.0, ay + 1.5, toe_x, toe_y, 2.2, 1.6, tone)
    draw_segment(layer, kx, ky, ax, ay, 2.6, 2.0, tone)
    draw_segment(layer, hx, hy, kx, ky, 3.1, 2.7, tone)
    return layer, max(ay + 3.5, toe_y + 1.6)     # lowest pixel row of the foot

def side_frames_drawn(img, keypoints, keep_above, ground, swing_deg, bend_deg):
    """Side view with legs redrawn from scratch (the source legs overlap and
    cannot be separated). Rows above `keep_above` (torso, underwear, the hand
    over the thigh) are kept; legs are drawn from a hip pivot hidden under
    them. The body drops so the lowest foot always touches the ground."""
    kp = keypoints
    pal = leg_palette(img, keep_above)
    near_tone = {'light': pal['light'], 'mid': pal['mid'], 'shadow': pal['shadow'], 'line': pal['line']}
    far_tone = {'light': pal['mid'], 'mid': pal['shadow'], 'shadow': pal['deep'], 'line': pal['line']}
    hip = ((kp['RIGHT HIP'][0] + kp['LEFT HIP'][0]) / 2, keep_above - 3.0)
    knee_y = (kp['RIGHT KNEE'][1] + kp['LEFT KNEE'][1]) / 2
    ankle_y = ground - 4.5
    thigh_len, shin_len = knee_y - hip[1], ankle_y - knee_y
    body = blank()
    for y in range(keep_above):
        body[y] = img[y][:]
    frames = []
    for s, c in phases():
        legs, lowest = [], 0.0
        for sign, tone, dx in ((-1, far_tone, -1.0), (1, near_tone, 1.0)):
            thigh_angle = math.radians(swing_deg * sign * s)
            knee_bend = math.radians(bend_deg * max(0.0, sign * c))
            layer, low = drawn_leg((hip[0] + dx, hip[1]), thigh_len, shin_len,
                                   thigh_angle, knee_bend, tone)
            legs.append(layer)
            lowest = max(lowest, low)
        drop = round(ground - lowest)            # stance foot back on the ground
        frame = compose(legs, body, 0)
        frames.append(shift(frame, drop) if drop else frame)
    return frames

def front_frames(img, top, knee, foot, split_x, lift, inward, swing_in):
    """Front/back view: while walking the feet come in toward the centre line
    (the wide stance is only for standing still). The swinging leg bends a
    little at the knee and its foot moves further in, under the body, instead
    of the shin lifting straight up (which looked bow-legged because the
    source shins slant outward)."""
    body, legs = split(img, top)
    left, right = split_legs(legs, top, split_x)
    frames = []
    for s, c in phases():
        left_swing, right_swing = max(0.0, s), max(0.0, -s)
        left_leg = knee_lifted(
            sheared(left, top, foot, inward + swing_in * left_swing), knee, lift * left_swing)
        right_leg = knee_lifted(
            sheared(right, top, foot, -(inward + swing_in * right_swing)), knee, lift * right_swing)
        frames.append(compose([left_leg, right_leg], body, 1 if abs(s) < 0.3 else 0))
    return frames

# ---------------------------------------------------------------- main
# Side views (east/west) are NOT generated: in the standing art both legs
# overlap, so they cannot be separated cleanly. Draw walk_east by hand
# (template in KnightClient/ArtSource/Hero) and create west with --mirror.
BASE = ['south', 'south-east', 'east', 'north-east', 'north']
MIRROR = {'west': 'east', 'north-west': 'north-east', 'south-west': 'south-east'}

def strip_of(frames):
    return [sum((frames[i][y] for i in range(len(frames))), []) for y in range(CELL)]

def cells_of(strip):
    count = len(strip[0]) // CELL
    return [[row[i * CELL:(i + 1) * CELL] for row in strip] for i in range(count)]

def mirror(src_path, dst_path):
    """Flip each cell of a strip horizontally, keeping frame order."""
    frames = [flip(cell) for cell in cells_of(read_png(src_path))]
    write_png(dst_path, strip_of(frames))
    print('mirrored', os.path.basename(src_path), '->', os.path.basename(dst_path))

def main():
    if sys.argv[1] == '--mirror':
        mirror(sys.argv[2], sys.argv[3])
        return
    standing_dir, manifest_path, out_dir = sys.argv[1:4]
    preview = sys.argv[sys.argv.index('--preview') + 1] if '--preview' in sys.argv else None
    manifest = json.load(open(manifest_path, encoding='utf-8'))
    sources = next(a for a in manifest['rig']['animations'] if a['id'] == 'standing')['sources']
    os.makedirs(out_dir, exist_ok=True)

    strips = {}
    for direction in BASE:
        img = read_png(os.path.join(standing_dir, f'hero_standing_{direction}.png'))
        kp = {k['label']: (k['x'] * CELL, k['y'] * CELL) for k in sources[direction]['keypoints'][0]}
        hip = max(kp['RIGHT HIP'][1], kp['LEFT HIP'][1])
        arm = max(kp['RIGHT ARM'][1], kp['LEFT ARM'][1])
        foot = max(kp['RIGHT LEG'][1], kp['LEFT LEG'][1])
        knee = round(min(kp['RIGHT KNEE'][1], kp['LEFT KNEE'][1]))
        top = round(max(hip + 4, arm + 8))          # below the hanging hands/fingers
        split_x = round((kp['RIGHT HIP'][0] + kp['LEFT HIP'][0]) / 2)
        if direction in ('south', 'north'):
            frames = front_frames(img, top, knee, foot, split_x, lift=3,
                                  inward=5 if direction == 'south' else 4, swing_in=2)
        elif direction == 'east':
            frames = side_frames_drawn(img, kp, keep_above=88, ground=115,
                                       swing_deg=22, bend_deg=40)
        else:
            axis_y = 1 if direction.startswith('south') else -1
            frames = diagonal_frames(img, top, foot, split_x, swing=3, depth=2,
                                     lift=3, axis_y=axis_y)
        strips[direction] = frames
    for direction, source in MIRROR.items():
        strips[direction] = [flip(f) for f in strips[source]]

    for direction, frames in strips.items():
        write_png(os.path.join(out_dir, f'walk_{direction}_strip{FRAMES}.png'), strip_of(frames))
        print('wrote', direction)

    if preview:
        rows = []
        for direction in strips:
            rows.extend(strip_of(strips[direction]))
        view = [[p if p[3] else (235, 235, 235, 255) for p in row] for row in rows]
        write_png(preview, view)
        print('preview', preview)

if __name__ == '__main__':
    main()
