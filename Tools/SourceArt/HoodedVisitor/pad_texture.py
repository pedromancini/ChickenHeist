# Pads the hooded visitor's colour texture (Assets/ChickenHeistGenerated/Characters/HoodedVisitor/hooded_visitor_0.png).
# The supplied texture fills the gaps between its UV islands with skin colour. Texture filtering and mipmaps blend
# those gaps into the edges of the islands, so thin skin-coloured lines showed along the hoodie's seams (shoulders,
# back, sleeves). Every texel outside the islands, and a one-texel ring just inside their edges, takes the colour of
# the nearest texel inside an island.
# The UV triangles come from build_visitor.py (generated/visitor_uv.npy).
# Run:  python pad_texture.py
import os
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = os.path.dirname(os.path.abspath(__file__))
TEXTURE = os.path.abspath(os.path.join(ROOT, '..', '..', '..', 'Assets', 'ChickenHeistGenerated', 'Characters', 'HoodedVisitor', 'hooded_visitor_0.png'))
tri = np.load(os.path.join(ROOT, 'generated', 'visitor_uv.npy'))
image = Image.open(TEXTURE).convert('RGBA'); N = image.size[0]
mask = Image.new('L', image.size, 0); draw = ImageDraw.Draw(mask)
for t in tri: draw.polygon([(u * N, (1 - v) * N) for u, v in t], fill=255)
inside = ndimage.binary_erosion(np.array(mask) > 0, iterations=1)
_, (iy, ix) = ndimage.distance_transform_edt(~inside, return_indices=True)
pixels = np.array(image)
padded = pixels[iy, ix]
print('islands cover %.1f%% of the texture; %d texels repainted' % (inside.mean() * 100, (~inside).sum()))
Image.fromarray(padded).save(TEXTURE)
