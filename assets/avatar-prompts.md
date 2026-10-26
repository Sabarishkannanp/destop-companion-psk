# Avatar provenance and prompts

Generated using the built-in imagegen tool with Selva's attached photograph as the identity reference. The original photo was supplied in chat and is not stored in this repository. These sprites do not come from a live run of the API-based AvatarGen tool.

Idle prompt: Transform the provided image into a visibly cartoon 3D animated film character for a desktop pet. Remove the entire background to true alpha transparency. Pixar character proportions: large head, big expressive eyes, simplified smooth face, compact torso, shorter limbs, stylized hair clumps. Preserve identity, brown skin, black hair, navy denim shirt, dark jeans, white shoes. Full body standing front relaxed smiling, hands outside pockets, feet near bottom. No photograph, background, shadow, or text.

Pose prompt prefix: Desktop companion 3D cartoon sprite, use reference character to preserve exact identity, face, brown skin, swept black hair, beard, navy denim shirt, dark jeans, white sneakers, stylized large head and compact body. Change only pose. Full body from head to shoes fully visible, centered, portrait 2:3 canvas. Transparent alpha background; isolated character with no colored background, haze, glow, ground shadow, or text. Soft lighting only on character. One character.

Pose suffixes:

1. `walk_right_1.png`: walking right in side view, left leg forward, right leg back, opposite arm swing.
2. `walk_right_2.png`: walking right in side view, legs passing underneath the body, mid stride.
3. `walk_right_3.png`: walking right in side view, right leg forward, left leg back, opposite arm swing.
4. `walk_right_4.png`: walking right in side view, legs passing, trailing heel raised, slight bounce.
5. `drink_water.png`: front facing smiling, holding a clear water bottle up.
6. `wave.png`: front facing smiling, waving with right hand.
7. `sad.png`: front facing slightly disappointed, shoulders down.

The idle sprite is the reference for every subsequent pose. Canvas normalization, cropping, and app/tray icon generation are deterministic in AvatarGen.

## Revised walking cycle

The original four independent poses were too similar. The current walk comes from the single built-in-generated `assets/walk-cycle.png` animation sheet, imported with `--import-walk-sheet`. A common scale preserves character size across the eight cells.

Sheet prompt: One professional transparent 3D animation sprite sheet, exactly four columns and two rows, same man and outfit in every cell, all full bodies facing right. Complete walk cycle: left contact, left planted/right push-off, right knee passing, right foot swing forward, right contact, right planted/left push-off, left knee passing, left foot swing forward. Clear different leg positions, consistent torso, scale and baseline, opposing arm swing, all characters within their cells, no labels, drawn grid, scenery, or ground shadow. Use the idle portrait as identity reference.
