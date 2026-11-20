# Original battle effects

These sixteen short effects were synthesized specifically for Paper Defense with no third-party recordings or sample libraries. The reproducible source is `tools/Audio/Generate-BattleAudio.py` at the project root (Python standard library only).

Dry wood/cardboard transients, damped spring resonances and filtered noise give weapons a small mechanical/paper character. Separate cues cover firing families, impacts, construction, modernization, castle danger, wave start and results. Files are 44.1 kHz mono PCM16, 0.14–1.12 seconds, with faded endpoints and peak headroom of at least 6 dB. Total source size: 610,166 bytes.

The runtime `PlayerBattleAudio` pool supplies levels, stereo position, priority and rate limiting. Do not normalize imported clips again or turn on looping. Keep them as short preloaded PCM effects. These assets and their generator follow the project's license.
