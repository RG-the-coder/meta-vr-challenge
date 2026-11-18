using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>Shared procedural combat sounds and bounded spatial voices; no per-unit AudioSources.</summary>
    public sealed class BattleAudio : MonoBehaviour
    {
        public const int VoiceCapacity=32;
        public bool Muted { get; private set; }
        public int VoiceCount => voices.Length;
        readonly AudioSource[] voices=new AudioSource[VoiceCapacity];
        readonly Dictionary<BattleUnit,Vector3> previousSteps=new Dictionary<BattleUnit,Vector3>();
        readonly List<AudioClip> clips=new List<AudioClip>();
        BattleDirector director;
        PossessionController player;
        AudioClip step, heavyStep, slash, arrow, repeater, impact, spell, healing, heavy;
        float nextSteps;
        int nextVoice;
        public void Initialize(BattleDirector owner)
        {
            director=owner;
            step=Make("Gravel step",.12f,85,.85f,23);
            heavyStep=Make("Armored step",.19f,53,.65f,18);
            slash=Make("Blade and air",.22f,420,.75f,13);
            arrow=Make("Bow release",.18f,230,.45f,25);
            repeater=Make("Mechanical report",.13f,115,.95f,28);
            impact=Make("Iron impact",.17f,760,.65f,25);
            spell=Make("Arcane discharge",.55f,390,.15f,7);
            healing=Make("Restoration chime",.65f,660,.025f,6);
            heavy=Make("Siege strike",.4f,48,.75f,11);
            for(int i=0;i<voices.Length;i++)
            {
                var obj=new GameObject("Combat voice "+i,typeof(AudioSource)); obj.transform.SetParent(transform,false);
                var source=obj.GetComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=1;
                source.rolloffMode=AudioRolloffMode.Linear; source.minDistance=2; source.maxDistance=42; source.dopplerLevel=0;
                voices[i]=source;
            }
            owner.UnitAttacked+=Attack; owner.AbilityActivated+=Ability; owner.UnitDamaged+=Hit; owner.RoundReset+=ResetSteps; owner.UnitDied+=ForgetFootsteps;
        }
        AudioClip Make(string name,float duration,float frequency,float noise,float decay)
        {
            const int rate=22050; int samples=Mathf.CeilToInt(duration*rate); var data=new float[samples]; uint seed=62137;
            float filtered=0;
            for(int i=0;i<samples;i++)
            {
                float t=i/(float)rate; seed=unchecked(seed*1664525u+1013904223u);
                float random=(seed&65535)/32767.5f-1; filtered=filtered*.72f+random*.28f;
                float tone=Mathf.Sin(2*Mathf.PI*frequency*t*(1-.22f*t/duration))*.65f+Mathf.Sin(2*Mathf.PI*frequency*1.5f*t)*.2f;
                float envelope=Mathf.Min(1,t*900)*Mathf.Exp(-t*decay)*Mathf.Clamp01((duration-t)*90);
                data[i]=Mathf.Clamp((tone*(1-noise)+filtered*noise)*envelope*.65f,-1,1);
            }
            var clip=AudioClip.Create(name,samples,1,rate,false); clip.SetData(data,0); clips.Add(clip); return clip;
        }
        void Play(AudioClip clip,Vector3 position,float volume,float pitch=1)
        {
            if(Muted || !isActiveAndEnabled || clip==null) return;
            if(player==null) player=director.GetComponent<PossessionController>();
            if(player!=null && (position-player.ViewCamera.transform.position).sqrMagnitude>42*42) return;
            AudioSource voice=null;
            for(int i=0;i<voices.Length;i++) { int index=(nextVoice+i)%voices.Length; if(!voices[index].isPlaying) { voice=voices[index]; nextVoice=(index+1)%voices.Length; break; } }
            if(voice==null) { voice=voices[nextVoice]; nextVoice=(nextVoice+1)%voices.Length; }
            voice.transform.position=position; voice.clip=clip; voice.pitch=pitch; voice.volume=volume*.65f; voice.Play();
        }
        void Attack(BattleUnit unit)
        {
            var role=unit.Definition.Role;
            AudioClip clip=unit.IsMelee?(role==UnitRole.Titan || role==UnitRole.Brute?heavy:slash)
                :role==UnitRole.Ranger || role==UnitRole.Militia?arrow:role==UnitRole.Skirmisher || role==UnitRole.Engineer || role==UnitRole.Bombard || role==UnitRole.Sharpshooter?repeater:spell;
            Play(clip,unit.EyePosition,.65f,1+(Mathf.Abs(unit.transform.GetSiblingIndex())%7-3)*.025f);
        }
        void Ability(BattleUnit unit,int slot)
        { Play(BattleClassCatalog.IsSupport(unit.Definition.Role)?healing:unit.IsMelee?heavy:spell,unit.EyePosition,.72f,1+slot*.08f); }
        void Hit(BattleUnit target,BattleUnit source,float amount) { Play(impact,target.AimPosition,.45f,target.Definition.IsMelee? .85f:1.12f); }
        void ForgetFootsteps(BattleUnit unit) => previousSteps.Remove(unit);
        public void SummitBow(Vector3 position) => Play(arrow, position, .65f);
        public void ToggleMute()
        { Muted=!Muted; if(Muted) foreach(var source in voices) if(source!=null) source.Stop(); }
        void ResetSteps() { previousSteps.Clear(); foreach(var source in voices) if(source!=null) source.Stop(); }
        void Update()
        {
            if(director==null || !director.IsBattleRunning || Time.time<nextSteps) return;
            nextSteps=Time.time+.12f;
            foreach(var unit in director.Units)
            {
                if(unit==null || !unit.IsAlive) continue;
                if(!previousSteps.TryGetValue(unit,out var previous)) { previousSteps[unit]=unit.transform.position; continue; }
                if((unit.transform.position-previous).sqrMagnitude<1.4f) continue;
                previousSteps[unit]=unit.transform.position;
                if(unit.Definition.Role==UnitRole.Wisp) continue;
                bool armored=unit.Definition.Role==UnitRole.Titan || unit.Definition.Role==UnitRole.Brute || unit.Definition.Role==UnitRole.Sentinel;
                Play(armored?heavyStep:step,unit.transform.position,.3f,armored? .8f:1.1f);
            }
        }
        void OnDisable() { foreach(var source in voices) if(source!=null) source.Stop(); }
        void OnDestroy()
        {
            if(director!=null) { director.UnitAttacked-=Attack; director.AbilityActivated-=Ability; director.UnitDamaged-=Hit; director.RoundReset-=ResetSteps; director.UnitDied-=ForgetFootsteps; }
            foreach(var clip in clips) if(clip!=null) Destroy(clip);
        }
    }
}
