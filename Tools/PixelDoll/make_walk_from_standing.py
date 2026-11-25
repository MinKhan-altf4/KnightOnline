"""Generate a 4-frame walk cycle for a PixelDoll character from its standing
sprites, using the leg keypoints in manifest.json. Pure Python (no PIL).

Side/diagonal views: the leg region is duplicated and sheared forward/back
(stride), alternating with a "passing" frame. Front/back views: legs are lifted
alternately. A 1 px body bob is baked in. Mirrored directions are produced by
flipping their PixelDoll source direction, like PixelDoll's own export.

This is a stand-in for hand-drawn or PixelDoll-generated walk frames: legs
are transformed from the still pose, so expect a stiffer look than real art.

usage (from the repository root):
  python Tools/PixelDoll/make_walk_from_standing.py       KnightClient/Assets/_Project/Resources/Characters/Hero       <pixeldoll-export>/manifest.json       KnightClient/Assets/_Project/Resources/Characters/Hero       [--preview walk_preview.png]
"""
import json
import os
import struct
import sys
import zlib

CELL = 128
FRAMES = 4

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
def split(img, top):
    """Body (rows above `top`) and legs (rows from `top`)."""
    body, legs = blank(), blank()
    for y in range(CELL):
        for x in range(CELL):
            (legs if y >= top else body)[y][x] = img[y][x]
    return body, legs

def sheared(legs, top, foot, amount, lift=0):
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

def side_frames(img, top, foot, stride):
    body, legs = split(img, top)
    frames = []
    for phase in range(FRAMES):
        frame = blank()
        if phase % 2 == 0:
            # contact: back leg then front leg on top, legs spread
            sign = 1 if phase == 0 else -1
            back = sheared(legs, top, foot, -stride * sign)
            front = sheared(legs, top, foot, stride * sign)
            layers = [back, front] if phase == 0 else [front, back]
            for layer in layers:
                for y in range(CELL):
                    for x in range(CELL):
                        over(frame, x, y, layer[y][x])
            for y in range(CELL):
                for x in range(CELL):
                    over(frame, x, y, body[y][x])
            frames.append(shift(frame, 1))       # body low on contact
        else:
            # passing: legs together, swinging foot lifted
            lifted = sheared(legs, top, foot, 0, lift=3)
            for y in range(CELL):
                for x in range(CELL):
                    over(frame, x, y, lifted[y][x])
                    over(frame, x, y, body[y][x])
            frames.append(frame)
    return frames

def diagonal_frames(img, top, foot, split_x, stride):
    """Both legs visible: shear the left and right leg in opposite directions."""
    body, legs = split(img, top)
    left, right = blank(), blank()
    for y in range(top, CELL):
        for x in range(CELL):
            if legs[y][x][3]:
                (left if x < split_x else right)[y][x] = legs[y][x]
    frames = []
    for phase in range(FRAMES):
        frame = blank()
        if phase % 2 == 0:
            # legs spread outward in both contacts; the front leg (alternating)
            # sits 2 px lower, i.e. nearer the viewer in the 3/4 view
            front_left = phase == 0
            layers = [sheared(left, top, foot, -stride, lift=-2 if front_left else 2),
                      sheared(right, top, foot, stride, lift=2 if front_left else -2)]
            for layer in layers:
                for y in range(CELL):
                    for x in range(CELL):
                        over(frame, x, y, layer[y][x])
            for y in range(CELL):
                for x in range(CELL):
                    over(frame, x, y, body[y][x])
            frames.append(shift(frame, 1))
        else:
            # passing: the back leg (the one that swings next) lifted
            swing = left if phase == 1 else right
            stay = right if phase == 1 else left
            for layer in (sheared(stay, top, foot, 0), sheared(swing, top, foot, 0, lift=3)):
                for y in range(CELL):
                    for x in range(CELL):
                        over(frame, x, y, layer[y][x])
            for y in range(CELL):
                for x in range(CELL):
                    over(frame, x, y, body[y][x])
            frames.append(frame)
    return frames

def front_frames(img, top, knee, split_x, lift):
    body, legs = split(img, top)
    frames = []
    for phase in range(FRAMES):
        frame = blank()
        lifted_side = {0: 'left', 1: None, 2: 'right', 3: None}[phase]
        for y in range(top, CELL):
            for x in range(CELL):
                p = legs[y][x]
                if not p[3]:
                    continue
                side = 'left' if x < split_x else 'right'
                # bend at the knee: only the shin and foot rise
                dy = -lift if side == lifted_side and y >= knee else 0
                over(frame, x, y + dy, p)
        for y in range(CELL):
            for x in range(CELL):
                over(frame, x, y, body[y][x])
        frames.append(shift(frame, 1) if lifted_side else frame)
    return frames

# ---------------------------------------------------------------- main
BASE = ['south', 'south-east', 'east', 'north-east', 'north']
MIRROR = {'west': 'east', 'north-west': 'north-east', 'south-west': 'south-east'}

def main():
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
        if direction in ('south', 'north'):
            split_x = round((kp['RIGHT HIP'][0] + kp['LEFT HIP'][0]) / 2)
            frames = front_frames(img, top, knee, split_x, lift=5)
        elif direction == 'east':
            frames = side_frames(img, top, foot, stride=9)
        else:
            split_x = round((kp['RIGHT HIP'][0] + kp['LEFT HIP'][0]) / 2)
            frames = diagonal_frames(img, top, foot, split_x, stride=6)
        strips[direction] = frames
    for direction, source in MIRROR.items():
        strips[direction] = [flip(f) for f in strips[source]]

    for direction, frames in strips.items():
        strip = [sum((frames[i][y] for i in range(FRAMES)), []) for y in range(CELL)]
        write_png(os.path.join(out_dir, f'walk_{direction}_strip{FRAMES}.png'), strip)
        print('wrote', direction)

    if preview:
        order = ['south', 'south-east', 'east', 'north-east', 'north', 'north-west', 'west', 'south-west']
        rows = []
        for direction in order:
            for y in range(CELL):
                rows.append(sum((strips[direction][i][y] for i in range(FRAMES)), []))
        # checkerboard-free white background for viewing
        view = [[p if p[3] else (235, 235, 235, 255) for p in row] for row in rows]
        write_png(preview, view)
        print('preview', preview)

if __name__ == '__main__':
    main()
