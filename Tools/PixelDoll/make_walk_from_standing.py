"""Generate an 8-frame walk cycle for a PixelDoll character from its standing
sprites, using the leg keypoints in manifest.json. Pure Python (no PIL).

Covered directions (8 frames, sin/cos phase, 1 px body dip near contact):
  south, north          knees and feet come in toward the centre line while
                        walking (shin vertical under the knee); the stepping
                        thigh foreshortens so the knee rises
  south-east, north-east (and south-west, north-west mirrored)
                        natural stance kept; legs swing in opposite phase
                        along the walking axis, front leg lower/higher
  east (and west mirrored)
                        one clean leg is cut from the ORIGINAL pixels (the
                        far foot peeking out behind the ankle is removed);
                        for both legs the thigh bends gradually out of the
                        body (no seam, +/-3 px at the knee) and the shin
                        swings +/-14 deg about the knee (swing leg bends up
                        to 22 deg). The far leg moves 75% and is a flat
                        shadow shape (outer outline only), as in pixel-art
                        walk cycles, so it reads as behind.
                        The body drops so the stance foot stays grounded.
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

def darken(p, factor):
    return (round(p[0] * factor), round(p[1] * factor), round(p[2] * factor), p[3])

def lowest_row(layer):
    for y in range(CELL - 1, -1, -1):
        if any(p[3] for p in layer[y]):
            return y
    return 0

def single_leg(img, keypoints, keep_above, thigh_half=3.5, shin_half=3.0, centre=True):
    """Cut ONE leg out of the side-view standing art, which shows the near leg
    plus the far thigh beside it and the far foot behind the ankle. Keep only
    pixels within half a leg width of the near leg's bones (hip -> knee ->
    ankle) on the far side, drop the far foot, and close every cut with the
    line colour. Both walk legs are built from this same shape, so they end
    up the same size and the near leg no longer carries the far thigh."""
    kp = keypoints
    near, far = ('RIGHT', 'LEFT') if kp['RIGHT LEG'][1] >= kp['LEFT LEG'][1] else ('LEFT', 'RIGHT')
    hip, knee, ankle = kp[f'{near} HIP'], kp[f'{near} KNEE'], kp[f'{near} LEG']
    far_x = kp[f'{far} KNEE'][0]
    toward_far = 1 if far_x >= knee[0] else -1     # side where the far leg sits
    far_ankle_y = kp[f'{far} LEG'][1]
    line = outline_color(img)

    def centre_x(y):
        a, b = (hip, knee) if y <= knee[1] else (knee, ankle)
        t = 0.0 if b[1] == a[1] else max(0.0, min(1.0, (y - a[1]) / (b[1] - a[1])))
        return a[0] + (b[0] - a[0]) * t

    leg = blank()
    for y in range(keep_above, CELL):
        leg[y] = img[y][:]
    for y in range(keep_above, CELL):
        if y <= ankle[1]:
            half = thigh_half if y <= knee[1] else shin_half
            limit = centre_x(y) + toward_far * half
        elif y <= ankle[1] + 1:
            limit = None
        else:
            continue                                    # near foot: keep whole
        if y >= far_ankle_y - 1 and y <= ankle[1]:      # far foot behind the ankle
            limit = ankle[0] + toward_far * 3.5 if limit is None else min(limit, ankle[0] + 3.5)                 if toward_far > 0 else max(limit, ankle[0] - 3.5)
        if limit is None:
            continue
        edge = round(limit)
        cut = range(edge + 1, CELL) if toward_far > 0 else range(0, edge)
        removed = False
        for x in cut:
            if leg[y][x][3]:
                removed = True
            leg[y][x] = EMPTY
        if removed and 0 <= edge < CELL and leg[y][edge][3]:
            leg[y][edge] = line
    if not centre:
        return leg
    # The far thigh sat on the facing side, so the cut leg is set back from
    # where the pair of legs was. Move each row to the middle of the near and
    # far bones so the walk frames line up with the standing body.
    far_hip, far_knee, far_ankle = kp[f'{far} HIP'], kp[f'{far} KNEE'], kp[f'{far} LEG']

    def far_centre_x(y):
        a, b = (far_hip, far_knee) if y <= far_knee[1] else (far_knee, far_ankle)
        t = 0.0 if b[1] == a[1] else max(0.0, min(1.0, (y - a[1]) / (b[1] - a[1])))
        return a[0] + (b[0] - a[0]) * t

    centred = blank()
    for y in range(keep_above, CELL):
        row_y = min(y, ankle[1])
        dx = round((far_centre_x(row_y) - centre_x(row_y)) / 2)
        for x in range(CELL):
            if leg[y][x][3] and 0 <= x + dx < CELL:
                centred[y][x + dx] = leg[y][x]
    return centred

def bent_leg(src, seam, knee, knee_shift, shin_angle, tint=1.0):
    """Side-view leg attached to the body without a seam: the thigh (rows
    seam..knee) bends gradually (0 px at the seam, `knee_shift` px at the
    knee), the shin and foot rotate rigidly about the shifted knee. Inverse
    mapping + nearest sample, so there are no holes."""
    kx, ky = knee
    kx2 = kx + knee_shift
    cs, ss = math.cos(shin_angle), math.sin(shin_angle)
    span = max(1.0, ky - seam)
    out = blank()
    for y in range(seam, CELL):
        for x in range(CELL):
            px, py = x + 0.5, y + 0.5
            sample = None
            # shin: inverse-rotate about the shifted knee
            ox, oy = px - kx2, py - ky
            sx = kx + ox * cs - oy * ss
            sy = ky + ox * ss + oy * cs
            ix, iy = int(math.floor(sx)), int(math.floor(sy))
            if ky <= iy < CELL and 0 <= ix < CELL and src[iy][ix][3]:
                sample = src[iy][ix]
            elif py < ky + 1:
                # thigh: undo the gradual horizontal bend of this row
                t = max(0.0, min(1.0, (py - seam) / span))
                ix = int(math.floor(px - knee_shift * t))
                if seam <= y < ky and 0 <= ix < CELL and src[y][ix][3]:
                    sample = src[y][ix]
            if sample is not None:
                out[y][x] = sample if tint == 1.0 else darken(sample, tint)
    return out

def flat_far_leg(layer, fill, line):
    """Far leg as a pixel-art 'behind' shape: one flat shadow fill with only
    the outer outline, no inner muscle lines (those stacked on the near leg's
    lines and read as extra legs)."""
    out = blank()
    for y in range(CELL):
        for x in range(CELL):
            if not layer[y][x][3]:
                continue
            edge = any(not (0 <= x + dx < CELL and 0 <= y + dy < CELL) or
                       not layer[y + dy][x + dx][3]
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            out[y][x] = line if edge else fill
    return out

def skin_shadow(img, top):
    """A mid-dark skin tone from the leg area, for the far leg fill."""
    tones = [p for row in img[top:] for p in row if p[3] > 200 and sum(p[:3]) > 380]
    tones.sort(key=lambda p: sum(p[:3]))
    p = tones[len(tones) // 4] if tones else (190, 120, 100, 255)
    return (p[0], p[1], p[2], 255)

def side_frames_single(img, keypoints, keep_above, ground, knee_shift, shin_deg,
                       bend_deg, far_scale):
    """Side view: one clean leg (original pixels, far foot removed) used for
    both legs. Each thigh bends gradually out of the body (no seam), each shin
    swings about its knee; the swinging leg's knee bends. The far leg moves
    less (`far_scale`) and is slightly darker so it reads as behind. The body
    drops so the lowest foot stays on the ground."""
    kp = keypoints
    knee = ((kp['RIGHT KNEE'][0] + kp['LEFT KNEE'][0]) / 2,
            (kp['RIGHT KNEE'][1] + kp['LEFT KNEE'][1]) / 2)
    src = single_leg(img, kp, keep_above)
    far_fill, line = skin_shadow(img, keep_above), outline_color(img)
    body = blank()
    for y in range(keep_above):
        body[y] = img[y][:]
    frames = []
    for s, c in phases():
        legs = []
        for sign, scale in ((-1, far_scale), (1, 1.0)):
            swing = sign * s * scale
            bend = math.radians(bend_deg * max(0.0, sign * c) * scale)
            leg = bent_leg(src, keep_above, knee, knee_shift * swing,
                           math.radians(shin_deg * swing) - bend)
            legs.append(flat_far_leg(leg, far_fill, line) if sign < 0 else leg)
        frame = compose(legs, body, 0)
        drop = ground - max(lowest_row(legs[0]), lowest_row(legs[1]))
        frames.append(shift(frame, drop) if drop else frame)
    return frames

def clean_shade(leg, base, shade, line, back_dx=-1, open_top=None):
    """Re-shade a leg for small sizes: outer outline, one flat skin tone, and a
    1 px shadow column along the back edge (the side at `back_dx`). The
    source's vertical muscle lines are dropped: at game size they split each
    leg into two thin ones, so two legs read as four."""
    out = blank()
    def opaque(x, y):
        # rows above `open_top` belong to the body: the leg continues into
        # them, so the seam row gets no horizontal outline
        if open_top is not None and y < open_top:
            return True
        return 0 <= x < CELL and 0 <= y < CELL and leg[y][x][3] > 0
    for y in range(CELL):
        for x in range(CELL):
            if not leg[y][x][3]:
                continue
            if any(not opaque(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out[y][x] = line
            elif not opaque(x + 2 * back_dx, y) or not all(
                    opaque(x + back_dx, y + dy) for dy in (-1, 0, 1)):
                out[y][x] = shade
            else:
                out[y][x] = base
    return out

def leg_tones(img, top):
    """Main skin tone and a shadow tone from the leg area of the source."""
    counts = {}
    for row in img[top:]:
        for p in row:
            if p[3] > 200 and sum(p[:3]) > 380:
                counts[p] = counts.get(p, 0) + 1
    ranked = sorted(counts, key=counts.get, reverse=True)
    base = ranked[0] if ranked else (249, 207, 174, 255)
    darker = [p for p in ranked if sum(p[:3]) < sum(base[:3]) - 120]
    shade = darker[0] if darker else darken(base, 0.8)
    return base, shade

def side_frames_scissor(img, keypoints, keep_above, foot, stride, lift, far_scale, far_tint):
    """Side view in the same style as the diagonal walk: straight legs that
    scissor in opposite phase from the hip (0 px at the seam, `stride` px at
    the foot), the swinging foot lifted a little, 1 px body dip near
    contact. Both legs come from one clean leg cut from the original pixels;
    the far leg moves `far_scale` of the near leg. Legs are re-shaded flat
    (clean_shade) and the far leg uses the skin shadow tone."""
    base, shade = leg_tones(img, keep_above)
    line = outline_color(img)
    leg = single_leg(img, keypoints, keep_above)
    src = clean_shade(leg, base, shade, line, open_top=keep_above)
    # far leg: same shape, skin shadow tone (keeps the skin hue, unlike
    # darkening every channel, which turned it grey-brown)
    far_src = clean_shade(leg, shade, darken(shade, far_tint), line, open_top=keep_above)
    body = blank()
    for y in range(keep_above):
        body[y] = img[y][:]
    frames = []
    for s, c in phases():
        far = sheared(far_src, keep_above, foot, -stride * far_scale * s,
                      lift * max(0.0, -c))
        near = sheared(src, keep_above, foot, stride * s, lift * max(0.0, c))
        frames.append(compose([far, near], body, body_bob(s)))
    return frames

def warp_leg(leg, top, knee, inward, lift):
    """Front/back leg warp: the thigh (top..knee) leans in by `inward` and is
    foreshortened by `lift` px (knee coming forward reads as the knee rising);
    from the knee down the leg is translated, staying straight and vertical
    so the knee sits right above the foot."""
    out = blank()
    span = max(1, knee - top)
    last_dest = None
    for y in range(top, CELL):
        t = min(1.0, (y - top) / span)
        dx = round(inward * t)
        dest = y - round(lift * t)
        rows = [dest] if last_dest is None else list(range(last_dest + 1, dest + 1)) or [dest]
        last_dest = dest
        for x in range(CELL):
            if leg[y][x][3]:
                for row in rows:
                    over(out, x + dx, row, leg[y][x])
    return out

def front_frames(img, top, knee, split_x, lift, inward, swing_in):
    """Front/back view: knees and feet both come in toward the centre line
    while walking (the wide stance is only for standing). The stepping leg's
    thigh foreshortens (knee forward and up) and moves a little further in;
    the shin stays straight under the knee."""
    body, legs = split(img, top)
    left, right = split_legs(legs, top, split_x)
    frames = []
    for s, c in phases():
        left_step, right_step = max(0.0, s), max(0.0, -s)
        left_leg = warp_leg(left, top, knee, inward + swing_in * left_step, lift * left_step)
        right_leg = warp_leg(right, top, knee, -(inward + swing_in * right_step), lift * right_step)
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
            frames = front_frames(img, top, knee, split_x, lift=3,
                                  inward=1, swing_in=0)
        elif direction == 'east':
            frames = side_frames_scissor(img, kp, keep_above=88, foot=round(foot),
                                         stride=7, lift=3, far_scale=0.85, far_tint=0.85)
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
