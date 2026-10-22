async () => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const lab=window.morphogenesisLab;
  if(document.getElementById("playBtn").textContent.includes("Pause"))document.getElementById("playBtn").click();
  lab.applyPreset("maze");lab.stepMany(70);const first=lab.getState();lab.reset();lab.stepMany(25);lab.stepMany(45);const repeat=lab.getState();
  check("same seeded reset and execution chunking reproduce field",first.hash===repeat.hash&&first.iteration===repeat.iteration,{hash:first.hash,iteration:first.iteration});
  check("concentrations are bounded and metrics finite",Object.values(repeat.metrics).every(Number.isFinite)&&repeat.metrics.meanA>=0&&repeat.metrics.meanA<=1&&repeat.metrics.meanB>=0&&repeat.metrics.meanB<=1);
  for(const id of ["mitosis","coral","maze","worms","solitons"]){lab.applyPreset(id);const r=lab.stepMany(40);check("preset "+id+" evolves finite fields",Object.values(r.metrics).every(Number.isFinite));}
  const boundary=document.getElementById("boundary");boundary.value="sealed";boundary.dispatchEvent(new Event("change",{bubbles:true}));lab.reset();lab.stepMany(35);check("sealed boundary remains finite",Object.values(lab.getState().metrics).every(Number.isFinite));
  const baseline=lab.getState().hash;
  for(const [key,value] of Object.entries({feed:.05,kill:.07,diffA:1.2,diffB:.8,dt:.5,stepsPerFrame:23,boundary:"torus",seed:123,brushRadius:19,brushMode:"erase"})){
    const previous=config[key];config[key]=value;
    check("fingerprint covers future-affecting "+key,lab.getState().hash!==baseline);
    config[key]=previous;
  }
  const previousRng=rng.state;rng.nextUint();check("fingerprint covers RNG state",lab.getState().hash!==baseline);rng.state=previousRng;
  const previousPreset=currentPreset;currentPreset="custom";check("fingerprint covers reset preset",lab.getState().hash!==baseline);currentPreset=previousPreset;
  for(const [key,value] of Object.entries({palette:"coral",contrast:3,fieldView:"reaction"})){
    const previous=config[key];config[key]=value;check("fingerprint excludes display-only "+key,lab.getState().hash===baseline);config[key]=previous;
  }
  const download=downloadBlob,canvasBlob=ui.world.toBlob;
  try{
    let filename,exported;downloadBlob=(name,blob)=>{filename=name;exported=blob;};
    advanceIterations(1);const fresh=lab.getState().hash;
    check("export fixture leaves cached display hash stale",currentHash!==fresh);
    document.getElementById("exportStateBtn").click();const doc=JSON.parse(await exported.text());
    check("state filename matches current checkpoint fingerprint",filename===`morphogenesis-${doc.fingerprint.toLowerCase()}-i${doc.iteration}.json`&&doc.fingerprint===fresh);
    let finishPng;ui.world.toBlob=callback=>finishPng=callback;document.getElementById("savePngBtn").click();
    advanceIterations(1);finishPng(new Blob());
    check("image filename uses current fingerprint",filename===`morphogenesis-${fresh.toLowerCase()}-i${doc.iteration}.png`);
  }finally{downloadBlob=download;ui.world.toBlob=canvasBlob;}
  const saved=stateDocument(),custom=structuredClone(saved);
  custom.width=31;custom.height=23;
  const encode=value=>floatArrayToBase64(new Float32Array(31*23).fill(value));
  custom.fieldA=encode(.8);custom.fieldB=encode(.2);
  custom.config.brushMode="perturb";
  await importStateFile(new File([JSON.stringify(custom)],"custom.json"));
  const resolution=document.getElementById("resolution");
  check("imported custom resolution is offered and selected",resolution.value==="31x23"&&[...resolution.options].some(o=>o.value==="31x23"));
  check("checkpoint restores future brush mode",config.brushMode==="perturb"&&document.getElementById("brushMode").value==="perturb");
  document.getElementById("resetBtn").click();const reset=stateDocument();check("reset retains imported resolution",reset.width===31&&reset.height===23);
  await importStateFile(new File([JSON.stringify(custom)],"custom.json"));
  check("reimport does not duplicate custom resolution",[...resolution.options].filter(o=>o.value==="31x23").length===1);
  chemicalShock();const future=lab.getState().hash;
  await importStateFile(new File([JSON.stringify(custom)],"custom.json"));chemicalShock();
  check("checkpoint restores future random interventions",lab.getState().hash===future);
  const legacy=structuredClone(custom);delete legacy.config.brushMode;
  await importStateFile(new File([JSON.stringify(legacy)],"legacy.json"));
  check("legacy checkpoints default to injection brush",config.brushMode==="inject"&&document.getElementById("brushMode").value==="inject");
  const invalid=structuredClone(custom);invalid.config.brushMode="invalid";
  const previous=lab.getState();let rejected=false;try{await importStateFile(new File([JSON.stringify(invalid)],"invalid.json"));}catch{rejected=true;}
  check("invalid brush checkpoint rejected without mutation",rejected&&JSON.stringify(lab.getState())===JSON.stringify(previous));
  await importStateFile(new File([JSON.stringify(saved)],"restore.json"));
  // Export/import are exercised through real download and file-input controls in browser_smoke.py.
  return results;
}
