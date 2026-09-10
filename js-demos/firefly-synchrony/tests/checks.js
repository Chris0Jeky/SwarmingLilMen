() => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const {sim, fixedDt, PRESETS, runSelfTest}=window.__fireflyDemo;
  document.getElementById("playPause").click();
  const C=sim.constructor,settings={...sim.settings,count:49,seed:314159,noise:.07,topology:"spatial",range:.24};
  const state=s=>JSON.stringify({time:s.time,rng:s.rng.state,spare:s.rng.spare,theta:[...s.theta],omega:[...s.omega],x:[...s.x],y:[...s.y]});
  const step=(s,n)=>{for(let i=0;i<n;i++)s.step(fixedDt);};
  check("built-in deterministic self-check",runSelfTest());
  let a=new C(settings),b=new C(settings);step(a,120);step(b,50);step(b,70);
  check("same seed and different chunk sizes match full numeric state",state(a)===state(b));
  const different=new C({...settings,seed:271828});step(different,120);
  check("different seed diverges",state(a)!==state(different));
  a=new C(settings);step(a,2);const cp=a.exportState();check("odd population exercises cached Gaussian state",Number.isFinite(cp.rngSpare));
  b=new C(settings);b.importState(cp);step(a,81);step(b,81);
  check("checkpoint restores future noisy evolution, including Gaussian cache",state(a)===state(b));
  for(const change of [d=>d.theta[0]=null,d=>d.settings.coupling=null,d=>d.settings.topology="invalid",d=>d.rngSpare=Infinity]){
    const bad=structuredClone(cp);change(bad);const before=state(b);let rejected=false;try{b.importState(bad);}catch{rejected=true;}
    check("invalid checkpoint rejected before live-state mutation",rejected&&before===state(b));
  }
  for(const topology of ["global","spatial","ring"]){const s=new C({...settings,topology,range:1});step(s,80);check(topology+" remains finite with bounded coherence",[...s.theta,...s.x,...s.y].every(Number.isFinite)&&s.order>=0&&s.order<=1+1e-12);if(topology==="spatial")check("full-radius metric query includes every other oscillator",s.meanNeighbors===48);}
  for(const [name,preset] of Object.entries(PRESETS)){const s=new C({...sim.settings,...preset.settings,count:48,seed:99});step(s,30);check("preset "+name+" runs",s.theta.every(Number.isFinite));}
  a.externalFlash();a.disrupt();a.regenerateFrequencies();step(a,20);check("interventions preserve finite phases",a.theta.every(Number.isFinite));
  return results;
}
