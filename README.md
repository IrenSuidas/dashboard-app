## Folder structure

```
Ending App
├── assets
|  ├── carousel        # Media for ending scene carousel
|  ├── fonts           # Fonts for rendering text
|  ├── images          # BG, overlay, outro emote, etc.
|  └── music           # Background music
├── config.yaml        # Main config file for the app
├── credits.yaml       # Credits text
├── EndingApp.exe
├── ffmpeg             # Library for decoding video
└── raylib.dll         # Library for gpu usage
```

### Assets

All images and videos for the carousel should go under `assets/carousel`.
It supports `.png`, `.jpg`, and `.mp4` formats.

Fonts should be placed in `assets/fonts` and should be `.ttf` format.
It already comes with fonts for JP text and symbols.
`georgia.ttf` is used for English text by default.

Background images, overlays, and outro emotes should go under `assets/images`.
Background music files should go under `assets/music` and should be `.mp3` format.

### Configuration

The main configuration file is `config.yaml`.
It includes modifications for text, images, positions, colors, etc, that get applied throughout the app.

The credits text is stored in `credits.yaml`.

### Scenes

#### Clip Scene

**Under construction**

#### Ending Scene

The ending scene contains all credits and are played by its configuration file.
All media from `assets/carousel` are used in this scene, rotated at random.
Under `config.yaml`, there's a config value of `endingStartDelay` which determines how long (in seconds) the app waits before starting to roll the credits.
<div>
  Press <kbd>Ctrl</kbd> + <kbd>Space</kbd> to return to the main menu during the ending scene.
</div>

#### Song Request Scene

**Under construction**
