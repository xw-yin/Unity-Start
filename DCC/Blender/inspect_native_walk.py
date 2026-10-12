"""Read the installed game's ordinary female gait using the local W3 tools."""
import sys,json
from pathlib import Path
sys.path[:0]=['F:/Unity/Tools/WitcherConversion','F:/Unity/Tools/WitcherConversion/site-packages']
import witcher3_tools as w3
prefs=w3._get_prefs();prefs.witcher_game_path='F:/SteamLibrary/steamapps/common/The Witcher 3'
prefs.uncook_path='F:/Unity/ExtractedModels/Characters';prefs.fbx_uncook_path=prefs.uncook_path
from witcher3_tools.CR2W.common_blender import repo_file
from witcher3_tools.CR2W.dc_anims import load_bin_anims_single,load_base_skeleton
source=repo_file(r'animations\woman\movement\woman_movement.w2anims')
rig='F:/Unity/ExtractedModels/Characters/characters/base_entities/woman_base/woman_base.w2rig'
print('NATIVE ANIMATION FILE',source)
base=load_base_skeleton(rig);print('RIG',base.__dict__.keys())
animset=load_bin_anims_single(source,anim_name='locomotion_walk_cycle_slow_forward',rigPath=rig)
for entry in animset.animations:
    anim=entry.animation;print('ANIMATION',anim.name,anim.__dict__.keys())
    buffer=anim.animBuffer;print('BUFFER',buffer.__dict__.keys())
    for bone in buffer.bones[:12]:print('BONE',bone.BoneName,str(bone.__dict__)[:450])
