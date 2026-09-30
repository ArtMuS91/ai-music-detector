# Fakeprint classifier weights

`weights.npz` is the logistic-regression model from
[lofcz/ai-music-detector](https://huggingface.co/lofcz/ai-music-detector) on Hugging Face,
revision `d2180598fed79e3f917e8050a00439982466e5c6`, MIT-licensed (see `LICENSE`).

It classifies the "fakeprint" of a track (the method from Afchar et al., "A Fourier Explanation
of AI-music Artifacts", ISMIR 2025) as real or AI-generated, and was trained on Suno <= 5 and
Udio <= 1.5 output (SONICS dataset plus proprietary data) against FMA Medium.

The feature extraction in `app/detectors/fakeprint.py` must stay exactly in step with the one
the model was trained with (`src/python/extract_fakeprints.py` in the author's GitHub repo):
16 kHz, 8192-point STFT, 1-8 kHz, 10-bin hull, residue clipped to 5 dB.
