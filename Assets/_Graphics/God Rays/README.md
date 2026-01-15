# God Rays Feature Explanation

## Get started

- Add to `manifest.json` in Packages this line:
  `"com.dbrizov.naughtyattributes": "https://github.com/dbrizov/NaughtyAttributes.git#upm"`
- Add `God Rays Feature` to list of Scriptalbe Render Features in `URP Renderer data`
- Create Global Volume with `God Rays Volume Component` on it

## Restrictions

- Names/paths of shaders and svc(shader variant collection) not recommended to change: I use it for initialization
  Shader Feature after first creating.
- `GodRaysSVC.shadervariants` should contains only two shaders: `Ledsna/GodRays` with selected count of iterations (
  shader feature `ITERATIONS_64` for example) and `Ledsna/BilaterialBlur` with empty keyword set
- To change parameters of effect create Global Volume and add `God Rays Volume Component`. Don't change default values
  in Render Feature
- Count iterations of loop in fragment shader(`SampleCount`) can't be changed in build version. SVC set up witch of
  shader variants with `ITERATIONS_X` will be used in build. It's automatically updates after changing in inspector
- If you want to disable God Rays Effect entirely from build you need:
    - Remove God Rays Feature
    - Clear `GodRaysSVC.shadervariants` all shaders if you also want to remove shader to add to build
- Work correct with orthographic camera. If use projection camera effect will be incorrect(not sure), but seems to be
  fine.
- Baked light not supported yet

## Features

- Effect fully compatable with unity volume system
- On Vulkan and Meta you get more performance as render feature use Framebuffer optimization that enabled on this APIs
- For friendly user experience I use package [`Naughty Attributes`](https://github.com/dbrizov/NaughtyAttributes):
    - Add to `manifest.json` in Packages this line:
      `"com.dbrizov.naughtyattributes": "https://github.com/dbrizov/NaughtyAttributes.git#upm"`
- If you want see effect in scene enable bool `renderInScene` in Render Feature settings

## Open problems

- Unity add to build shader `Ledsna/GodRays` with empty set of keywords. It's hard to understand why unity shader
  preprocessor thinks that that shader used(I tried remove serailization from Shader field `godRaysShader` but this
  didn't help). Probably I should use `IPreprocessShaders` but I think this overhead.
- Currently, I have two cases of God Rays usage:
    - Blur enabled: God Rays -> Bilaterial Blur X -> Bilaterial Blur Y -> Compositing
    - Blur disabled: God Rays -> Compositing

  In first case I need save god rays to intermediate texture as I need firstly apply blur and then make composition.
  But for second case there is no need in composition pass, because I can directly apply effect to
  `cameraColorTarget`. For sample count like 64 or 86 noise is not that strong, so we can disable blur effect, but still
  get some overhead
- **\[SOLVED\]**In unity 6.0 version there is bug with method `Create` of `ScriptableRenderFeature`: it's not called
  when it should. So
  sometimes when you change scene you can see warning from code. To fix you need run in play mode.
    - We updated current version of unity to 6.1

## Change log

### 08/01/2025

- Fixed bug with moving god rays through transparent water. We need use `RenderPassEvent.BeforeRenderingPostProcessing`
  and set `Depth Texture Mode` to `After Transparent` in URP settings
- Refactor