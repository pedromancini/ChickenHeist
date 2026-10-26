import os
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = os.path.dirname(os.path.abspath(__file__))
TEXTURE = os.path.abspath(os.path.join(ROOT, '..', '..', '..', 'Assets', 'ChickenHeistGenerated', 'Characters', 'HoodedVisitor', 'hooded_visitor_0.png'))
EYE_COLOURS = int(os.environ.get('VISITOR_EYE_COLOURS', '4'))
EYE_SMOOTH = int(os.environ.get('VISITOR_EYE_SMOOTH', '7'))
tri = np.load(os.path.join(ROOT, 'generated', 'visitor_uv.npy'))
eyes_path = os.path.join(ROOT, 'generated', 'visitor_eyes.npy')
eyes = np.load(eyes_path) if os.path.exists(eyes_path) else np.zeros(len(tri), bool)
image = Image.open(TEXTURE).convert('RGBA'); N = image.size[0]
pixels = np.array(image)

ids = Image.new('RGB', image.size, (0, 0, 0)); draw = ImageDraw.Draw(ids)
for i, t in enumerate(tri):
    k = i + 1; draw.polygon([(u * N, (1 - v) * N) for u, v in t], fill=(k & 255, (k >> 8) & 255, (k >> 16) & 255))
code = np.array(ids).astype(np.int64); label = code[..., 0] | (code[..., 1] << 8) | (code[..., 2] << 16)
flat = label.ravel(); rgb = pixels[..., :3].reshape(-1, 3).astype(np.float64)
count = np.bincount(flat, minlength=len(tri) + 1)
mean = np.stack([np.bincount(flat, weights=rgb[:, c], minlength=len(tri) + 1) for c in range(3)], 1) / np.maximum(count, 1)[:, None]
painted = rgb.copy(); inside_any = flat > 0
painted[inside_any] = mean[flat[inside_any]]
eye_texel = inside_any & np.concatenate(([False], eyes))[flat]
if eye_texel.any():
    colours = rgb[eye_texel]; rng = np.random.default_rng(7)
    centres = colours[rng.choice(len(colours), EYE_COLOURS, replace=False)]
    for _ in range(20):
        nearest = np.argmin(((colours[:, None, :] - centres[None]) ** 2).sum(2), 1)
        centres = np.array([colours[nearest == c].mean(0) if (nearest == c).any() else centres[c] for c in range(EYE_COLOURS)])
    painted[eye_texel] = centres[nearest]
    index = np.full(label.shape, -1); index.ravel()[np.flatnonzero(eye_texel)] = nearest
    for _ in range(2):
        votes = np.stack([ndimage.uniform_filter((index == c).astype(float), size=EYE_SMOOTH) for c in range(EYE_COLOURS)])
        best = np.argmax(votes, 0); band = index >= 0; index[band] = best[band]
    painted[eye_texel] = centres[index.ravel()[eye_texel]]
pixels[..., :3] = painted.reshape(pixels.shape[0], pixels.shape[1], 3).clip(0, 255).astype(np.uint8)
print('%d facets painted flat, %d texels of the eye band in %d colours' % ((count[1:] > 0).sum(), eye_texel.sum(), EYE_COLOURS))

mask = Image.new('L', image.size, 0); draw = ImageDraw.Draw(mask)
for t in tri: draw.polygon([(u * N, (1 - v) * N) for u, v in t], fill=255)
inside = ndimage.binary_erosion(np.array(mask) > 0, iterations=1)
_, (iy, ix) = ndimage.distance_transform_edt(~inside, return_indices=True)
padded = pixels[iy, ix]
print('islands cover %.1f%% of the texture; %d texels repainted' % (inside.mean() * 100, (~inside).sum()))
Image.fromarray(padded).save(TEXTURE)
