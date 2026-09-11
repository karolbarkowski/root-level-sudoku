# Raster asset generation

Tool: built-in image_gen. Reference: the approved Sudoku Endless poster concept.

## paper-background.png

Create a production game background texture using the supplied image as a style reference only. Output ONLY a full-bleed rectangular sheet of the beautiful worn warm ivory paper seen in the reference. Tall portrait 1:2 aspect ratio, high resolution. Natural irregular fine paper fibers, faint diagonal scuffs, subtly dirty gray-beige patina and some rubbed edges. Match reference pale ivory tone, restrained grunge, authentic scanned vintage poster stock. Even diffuse illumination. NO text, NO letters, NO numeral, NO grid, NO buttons, NO logos, NO objects, NO shadows or folds that look like objects, NO border. This is a standalone reusable raster background asset, not a UI mockup. Use the paper surface's reference detail rather than synthetic random noise.

## nine-distressed.png

Initial generation:

Create a standalone production sprite asset: ONLY the giant distressed charcoal numeral '9' from this reference, isolated on a genuinely TRANSPARENT alpha background. No UI, no words, no title, no logo, no paper background, no frame. Restore the whole numeral without cropping: fully visible rounded upper edge and full lower curve with a small transparent margin all around. Shape: very bold heavy condensed sans-serif numeral, tall approximately 1:2 width-to-height, straight parallel outer left edges on top and lower hook, broad heavy right stem, tall narrow round-ended vertical upper counter and deep lower hook notch. Match reference numeral shape approximately, no taper, no thin elegant type. Match its incredible worn screenprint grunge: dark charcoal ink with many natural tiny scratches, irregular distressed rubbed patches, some diagonal hairline scuffs and subtly eroded edge. Tiny worn areas should be transparent so the paper layer beneath shows through, NOT painted white. Keep a solid substantial dark silhouette, not skeletal or overly damaged. The asset must have real transparency outside and in holes. No drop shadow. High resolution. Render this single numeral centered and entirely contained in a tall portrait transparent image.

The generator returned an opaque checkerboard. The final asset uses the following correction and standard multiply compositing (no custom shader):

Edit this image for use as a monochrome printing plate. Keep the entire distressed black numeral 9 EXACTLY as it is, same shape and position and complete uncropped top and bottom. Change EVERY checkerboard / gray background pixel outside the numeral and in both its open spaces to perfectly SOLID PURE WHITE RGB 255,255,255. This time the background MUST be opaque pure white, not transparency, absolutely no checkerboard and no paper texture. The worn holes and scratches in the ink should also be pure white. This is a black ink image on a perfectly flat white background for multiply compositing in a game. No other elements. Keep the exact same canvas proportions and margin. Black distressed numeral 9 on a uniform pure white background.
