() => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const {sim, fixedDt, PRESETS, runSelfTest}=window.__fireflyDemo;
  document.getElementById("playPause").click();
  const C=sim.constructor,settings={...sim.settings,count:49,seed:314159,noise:.07,topology:"spatial",range:.24};
  const state=s=>JSON.stringify({time:s.time,rng:s.rng.state,spare:s.rng.spare,theta:[...s.theta],omega:[...s.omega],x:[...s.x],y:[...s.y]});
  const step=(s,n)=>{for(let i=0;i<n;i++)s.step(fixedDt);};
  const range=document.getElementById("range"),topology=document.getElementById("topology");
  topology.value="ring";topology.dispatchEvent(new Event("change",{bubbles:true}));
  for(const value of [.03,.50,.75,1]){
    range.value=String(value);range.dispatchEvent(new Event("input",{bubbles:true}));
    const ring=new C({...settings,count:48,topology:"ring",range:sim.settings.range});
    const restored=new C(settings);restored.importState(ring.exportState());restored.computeRingCoupling();
    check("ring control/checkpoint span "+value,Number(range.value)===value&&restored.settings.range===value&&restored.meanNeighbors===Math.min(47,2*Math.max(1,Math.floor(48*value/2))));
    let matches=true;
    for(let i=0;i<48;i++){
      let sum=0,count=0;
      for(let j=0;j<48;j++)if(i!==j&&Math.min((j-i+48)%48,(i-j+48)%48)<=Math.max(1,Math.floor(48*value/2))){sum+=Math.sin(restored.theta[j]-restored.theta[i]-restored.settings.lag);count++;}
      matches=matches&&Math.abs(restored.couplingTerm[i]-sum/count)<1e-12;
    }
    check("ring coupling uses each neighbor once at span "+value,matches);
  }
  for(const count of [1,47,48]){
    const ring=new C({...settings,count,topology:"ring",range:1});ring.computeRingCoupling();
    check("full ring has unique neighbors for population "+count,ring.meanNeighbors===count-1&&ring.couplingTerm.every(Number.isFinite));
  }
  for(const topology of ["global","spatial","ring"]){
    const source=new C({...settings,topology,movement:.12}).exportState();
    source.x[0]=1;source.y[0]=1;source.theta[0]=-1000*Math.PI;
    source.vx[0]=-1000123;source.vy[0]=1000456;
    source.vx[1]=-Number.MAX_VALUE;source.vy[1]=Number.MAX_VALUE;
    const restored=new C(settings);restored.importState(source);
    const bounded=()=>[...restored.x,...restored.y].every(v=>v>=0&&v<1)&&restored.theta.every(v=>v>=0&&v<2*Math.PI);
    check(topology+" checkpoint normalizes position endpoint and phases",bounded());
    step(restored,3);check(topology+" imported finite velocities wrap across many worlds",bounded());
  }
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
