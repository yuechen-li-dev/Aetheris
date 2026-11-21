"""Painted graphics for the Claude-mobile fixture, in sRGB."""
from PIL import Image, ImageDraw, ImageFont


ORANGE = (232, 104, 48)
CREAM = (255, 232, 185)
INK = (51, 26, 44)
PANEL_SIZE = 512


class ArtPanel:
    def __init__(self, size, color):
        self.size = size
        self.image = Image.new("RGB", (size * 2, size * 2), color)
        self.draw = ImageDraw.Draw(self.image)

    def points(self, sequence):
        return [(round(x * self.size * 2), round(y * self.size * 2)) for x, y in sequence]

    def oval(self, box, color):
        left, top, right, bottom = box
        self.draw.ellipse(self.points(((left, top), (right, bottom))), fill=color)

    def polygon(self, points, color):
        self.draw.polygon(self.points(points), fill=color)

    def contour(self, start, curves, color):
        points = [start]
        for a, b, end in curves:
            p = points[-1]
            for tick in range(1, 25):
                t = tick / 24
                u = 1 - t
                points.append(tuple(u ** 3 * p[i] + 3 * u * u * t * a[i] + 3 * u * t * t * b[i] + t ** 3 * end[i]
                                    for i in range(2)))
        self.polygon(points, color)

    def spark(self, x, y, radius, color):
        self.polygon(((x, y - radius), (x + radius * 0.21, y - radius * 0.24),
                      (x + radius, y), (x + radius * 0.21, y + radius * 0.24),
                      (x, y + radius), (x - radius * 0.21, y + radius * 0.24),
                      (x - radius, y), (x - radius * 0.21, y - radius * 0.24)), color)

    def finish(self):
        return self.image.resize((self.size, self.size), Image.Resampling.LANCZOS)


def eye(outside):
    art = ArtPanel(PANEL_SIZE, ORANGE)
    art.oval((0.062, 0.082, 0.938, 0.918), (244, 133, 62))

    # Three tapered lashes fan out at the outer corner, behind the dark liner.
    def reflect(p):
        return (p[0] if outside > 0 else 1 - p[0], p[1])

    for root, tip in (((0.66, 0.29), (0.65, 0.098)), ((0.73, 0.31), (0.78, 0.13)),
                      ((0.78, 0.37), (0.89, 0.205))):
        x, y = root
        u, v = tip
        art.contour(reflect((x - 0.026, y)),
                    [tuple(map(reflect, ((x - 0.010, y - 0.071), (u + 0.027, v + 0.062), tip))),
                     tuple(map(reflect, ((u + 0.062, v + 0.058), (x + 0.028, y - 0.031), (x + 0.027, y + 0.018))))],
                    INK)

    art.oval((0.10, 0.219, 0.90, 0.815), INK)
    art.oval((0.134, 0.257, 0.866, 0.778), (255, 241, 211))
    art.oval((0.203, 0.286, 0.796, 0.734), (255, 249, 233))
    offset = -0.027 * outside
    art.oval((0.281 + offset, 0.282, 0.699 + offset, 0.754), (27, 57, 64))
    art.oval((0.319 + offset, 0.310, 0.663 + offset, 0.730), (40, 137, 125))
    art.oval((0.352 + offset, 0.390, 0.627 + offset, 0.711), (110, 192, 150))
    art.oval((0.363 + offset, 0.324, 0.618 + offset, 0.684), (30, 38, 53))
    art.oval((0.397 + offset, 0.356, 0.521 + offset, 0.498), (255, 255, 242))
    art.oval((0.553 + offset, 0.590, 0.609 + offset, 0.649), (217, 254, 234))
    return art.finish()


def front():
    art = ArtPanel(PANEL_SIZE, ORANGE)
    art.oval((0.091, 0.339, 0.217, 0.499), (247, 139, 104))
    art.oval((0.783, 0.339, 0.909, 0.499), (247, 139, 104))
    art.contour((0.168, 0.408), [
        ((0.248, 0.477), (0.364, 0.508), (0.500, 0.509)),
        ((0.636, 0.508), (0.752, 0.477), (0.832, 0.408)),
        ((0.790, 0.621), (0.674, 0.846), (0.500, 0.843)),
        ((0.326, 0.846), (0.210, 0.621), (0.168, 0.408)),
    ], INK)
    art.contour((0.276, 0.466), [
        ((0.410, 0.517), (0.590, 0.517), (0.724, 0.466)),
        ((0.700, 0.579), (0.591, 0.591), (0.500, 0.591)),
        ((0.409, 0.591), (0.300, 0.579), (0.276, 0.466)),
    ], (255, 237, 199))
    art.contour((0.399, 0.759), [
        ((0.418, 0.699), (0.476, 0.687), (0.510, 0.723)),
        ((0.549, 0.685), (0.610, 0.701), (0.627, 0.761)),
        ((0.559, 0.798), (0.466, 0.803), (0.399, 0.759)),
    ], (218, 89, 109))
    for x in (0.118, 0.142, 0.166, 0.834, 0.858, 0.882):
        art.oval((x - 0.005, 0.414, x + 0.005, 0.436), (255, 201, 146))
    return art.finish()


def hood():
    art = ArtPanel(PANEL_SIZE, ORANGE)
    # Tapered stripes, leaving the flame-orange fenders clear around the eyes.
    for side in (-1, 1):
        def x(a):
            return 0.5 + side * a
        art.contour((x(0.066), 0.098), [
            ((x(0.122), 0.101), (x(0.135), 0.200), (x(0.131), 0.289)),
            ((x(0.115), 0.459), (x(0.078), 0.667), (x(0.057), 0.876)),
            ((x(0.044), 0.800), (x(0.051), 0.536), (x(0.052), 0.224)),
            ((x(0.052), 0.154), (x(0.056), 0.115), (x(0.066), 0.098)),
        ], CREAM)
    art.spark(0.500, 0.240, 0.044, (255, 191, 108))
    return art.finish()


def side():
    art = ArtPanel(PANEL_SIZE, ORANGE)
    art.contour((0.071, 0.733), [
        ((0.200, 0.395), (0.649, 0.259), (0.931, 0.304)),
        ((0.909, 0.492), (0.824, 0.650), (0.721, 0.660)),
        ((0.440, 0.636), (0.231, 0.772), (0.071, 0.733)),
    ], (169, 49, 49))
    art.contour((0.070, 0.688), [
        ((0.218, 0.356), (0.656, 0.209), (0.935, 0.261)),
        ((0.917, 0.439), (0.820, 0.609), (0.716, 0.612)),
        ((0.455, 0.590), (0.208, 0.737), (0.070, 0.688)),
    ], CREAM)
    art.spark(0.236, 0.565, 0.061, (237, 121, 63))
    art.spark(0.847, 0.373, 0.064, (237, 121, 63))

    # Compensate for the long, shallow projection of the door panel.
    word = "Claude-mobile"
    font = ImageFont.load_default(size=120)
    xmin, ymin, xmax, ymax = font.getbbox(word, stroke_width=1)
    lettering = Image.new("L", (xmax - xmin, ymax - ymin))
    ImageDraw.Draw(lettering).text((-xmin, -ymin), word, font=font, stroke_width=1, fill=255)
    size = art.points(((0.443, 0.174),))[0]
    lettering = lettering.resize(size, Image.Resampling.LANCZOS)
    art.image.paste(INK, art.points(((0.326, 0.362),))[0], lettering)
    return art.finish()


def body_atlas():
    atlas = Image.new("RGB", (2 * PANEL_SIZE, 2 * PANEL_SIZE), ORANGE)
    atlas.paste(front(), (0, 0))
    atlas.paste(hood(), (PANEL_SIZE, 0))
    atlas.paste(side(), (0, PANEL_SIZE))
    return atlas
