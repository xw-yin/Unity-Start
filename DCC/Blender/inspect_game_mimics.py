"""Find original mimic animation banks in the installed game, read-only."""
import sys,json
from pathlib import Path
sys.path[:0]=['F:/Unity/Tools/WitcherConversion','F:/Unity/Tools/WitcherConversion/site-packages']
import witcher3_tools as w3
prefs=w3._get_prefs();prefs.witcher_game_path='F:/SteamLibrary/steamapps/common/The Witcher 3'
prefs.uncook_path='F:/Unity/ExtractedModels/Characters';prefs.fbx_uncook_path=prefs.uncook_path
from witcher3_tools.CR2W.witcher_cache.Bundles import LoadBundleManager
manager=LoadBundleManager()
paths=sorted(p for p in manager.Items if 'mimic' in p.lower() and p.endswith('.w2anims'))
output=Path('F:/Unity/Project/Start/Artifacts/FaceReview-20261021/game-mimic-banks.json');output.write_text(json.dumps(paths,indent=2))
print('MIMIC BANKS',len(paths),flush=True)
for path in paths:print(path,flush=True)
from witcher3_tools.CR2W.common_blender import repo_file
from witcher3_tools.CR2W.dc_anims import load_bin_anims_info
banks=[]
for path in [r'animations\mimics\yennefer_mimic_animation.w2anims',r'animations\woman\dialog\yennefer_mimic_layers.w2anims']:
 resolved=repo_file(path);info=load_bin_anims_info(resolved)
 clips=[{'name':e.animation.name,'duration':e.animation.duration} for e in info.animations]
 banks.append({'path':path,'file':resolved,'clips':clips});print('CLIPS',path,clips,flush=True)
(output.parent/'yennefer-mimic-clips.json').write_text(json.dumps(banks,indent=2))
