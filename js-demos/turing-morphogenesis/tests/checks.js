() => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const lab=window.morphogenesisLab;
  if(document.getElementById("playBtn").textContent.includes("Pause"))document.getElementById("playBtn").click();
  lab.applyPreset("maze");lab.stepMany(70);const first=lab.getState();lab.reset();lab.stepMany(25);lab.stepMany(45);const repeat=lab.getState();
  check("same seeded reset and execution chunking reproduce field",first.hash===repeat.hash&&first.iteration===repeat.iteration,{hash:first.hash,iteration:first.iteration});
  check("concentrations are bounded and metrics finite",Object.values(repeat.metrics).every(Number.isFinite)&&repeat.metrics.meanA>=0&&repeat.metrics.meanA<=1&&repeat.metrics.meanB>=0&&repeat.metrics.meanB<=1);
  for(const id of ["mitosis","coral","maze","worms","solitons"]){lab.applyPreset(id);const r=lab.stepMany(40);check("preset "+id+" evolves finite fields",Object.values(r.metrics).every(Number.isFinite));}
  const boundary=document.getElementById("boundary");boundary.value="sealed";boundary.dispatchEvent(new Event("change",{bubbles:true}));lab.reset();lab.stepMany(35);check("sealed boundary remains finite",Object.values(lab.getState().metrics).every(Number.isFinite));
  // Export/import are exercised through real download and file-input controls in browser_smoke.py.
  return results;
}
