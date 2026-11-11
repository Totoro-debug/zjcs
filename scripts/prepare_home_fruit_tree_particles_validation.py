"""Stage current fruit particles and a real-home rendering/repair regression."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_card_native import sha
from validation_workspace import validation_directory, require_temporary_unity_project


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    args = parser.parse_args()
    project = require_temporary_unity_project(args.project)
    receipt = json.loads((ROOT / 'assets/reference/home-fruit-tree-particles-source-manifest.json').read_bytes())
    for relative, digest in {**receipt['inputs'], **receipt['outputs']}.items():
        assert sha((ROOT / relative).read_bytes()) == digest, relative
    for relative in receipt['outputs']:
        target = project / Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / relative, target)
    relative = Path('Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs')
    shutil.copy2(ROOT / 'unity' / relative, project / relative)
    editor = project / 'Assets/Editor/HomeFurniture'
    assert (editor / 'HomeFurnitureValidation.asmdef').is_file(), 'Stage the real home furniture host first'
    source = (ROOT / 'scripts/fixtures/home_furniture_host_validation.cs').read_text('utf8')
    before = 'Require(home.Rules.FurnitureVersion == "884_156692"'
    source = source.replace(before, '''var particles = home.Scene.Root.transform.Find("FruitTree");
        Require(particles != null && particles.GetComponentsInChildren<ParticleSystem>(true).Length == 23,
            "Current original particle module count");
        var renderers = particles.GetComponentsInChildren<ParticleSystemRenderer>(true);
        Require(renderers.Length == 24, "Current original particle renderer count");
        int boundMaterials = 0, emptyMaterials = 0;
        foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) { emptyMaterials++; continue; }
                boundMaterials++;
                Require(material.shader != null && material.shader.isSupported &&
                    material.shader.name.StartsWith("Zjcs/Official/Home/FruitTree/"),
                    "Explicit current complete Windows particle shader");
            }
        Require(boundMaterials == 23 && emptyMaterials == 1,
            "Original 23 bound particle materials and one empty renderer material slot");
        Require(home.Rules.FurnitureVersion == "884_156692"''', 1)
    marker = 'Color sky = SkyPixel();'
    assert marker in source
    source = source.replace(marker, marker + '''
        Save(name.Replace(".png", "-world.png"));
        if (name.Contains("before-repair"))
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            pixel.ReadPixels(new Rect(target.width * .05f, target.height * .35f, 1, 1), 0, 0); pixel.Apply();
            Color value = pixel.GetPixel(0, 0); Object.Destroy(pixel); RenderTexture.active = previous;
            Require(value.r + value.g + value.b > .1f, "Original additive effect preserves scene instead of black opaque quad: " + value);
        }
''', 1)
    source = source.replace('home-current-', 'home-current-particles-')
    source = source.replace('HOME_FURNITURE_HOST_PASSED:', 'HOME_FRUIT_PARTICLES_HOST_PASSED:')
    (editor / 'HomeFurnitureHostValidation.cs').write_text(source, 'utf8', newline='\n')
    print('Staged current fruit particle closure and actual home host regression')


if __name__ == '__main__':
    main()
