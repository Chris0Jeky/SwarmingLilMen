() => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const lab=window.AvalancheLab;lab.pause();
  const run=seed=>{lab.reset({size:32,seed,prime:"critical"});for(let i=0;i<70;i++){lab.drive(1,"random");if(!lab.settle().complete)throw Error("relaxation limit exceeded");}return lab.snapshot();};
  const a=run(7331),b=run(7331),c=run(8831);
  check("same seed produces identical settled states",a.stateHash===b.stateHash);
  check("different seeds diverge",a.stateHash!==c.stateHash);
  check("grain conservation after repeated avalanches",a.accountingResidual===0&&b.accountingResidual===0&&c.accountingResidual===0);
  lab.reset({size:32,seed:5,prime:"clear"});lab.addGrains(16,16,1000);let activeRejected=false;try{lab.createCheckpoint();}catch{activeRejected=true;}
  check("active-avalanche export is rejected",activeRejected);const settled=lab.settle();check("large perturbation fully relaxes with exact mass accounting",settled.complete&&settled.snapshot.accountingResidual===0&&settled.snapshot.unstableCells===0,{topplings:settled.processed});
  const cp=lab.createCheckpoint(),hash=lab.snapshot().stateHash;lab.drive(15,"center");lab.settle();check("perturbation changes checkpoint state",lab.snapshot().stateHash!==hash);lab.applyCheckpoint(cp);check("stable checkpoint round-trip",lab.snapshot().stateHash===hash);
  lab.drive(1,"random");lab.settle();const future=lab.snapshot().stateHash;lab.applyCheckpoint(cp);lab.drive(1,"random");lab.settle();check("checkpoint restores future random drive",future===lab.snapshot().stateHash);
  const stableState=()=>{const checkpoint=lab.createCheckpoint();delete checkpoint.exportedUtc;return JSON.stringify({checkpoint,snapshot:lab.snapshot(),summary:lab.summary(),seed:document.getElementById("seed").value,grid:document.getElementById("gridSize").value});};
  const badFields=[["history",[null]],["history",{}],["history",[{...cp.history[0],topplings:-1}]],["counters",[]],["controls",[]],["controls",{driveMode:"unknown"}],["controls",{driveRate:{}}],["controls",{grainsPerEvent:9}],["controls",{toppleBudget:4}],["controls",{showActivity:"true"}],["seed",{}],["rngState",{}]];
  for(const key of Object.keys(cp.counters))badFields.push(["counters",{[key]:"invalid"}]);
  for(const [index,[field,value]] of badFields.entries()){
    const before=stableState(),malformed=JSON.parse(JSON.stringify(cp));
    malformed.width=16;malformed.height=16;malformed.heights=Array(256).fill(2);malformed.sinks=Array(256).fill(0);malformed.seed=91;malformed[field]=value;
    let rejected=false;try{lab.applyCheckpoint(malformed);}catch{rejected=true;}
    check(`malformed checkpoint ${field} (${index}) is rejected without changing live state`,rejected&&stableState()===before);
  }
  const replayStart=lab.createCheckpoint();
  lab.addGrains(16,16,300);lab.process(7);
  const activeBefore=JSON.stringify(lab.snapshot()),summaryBefore=lab.summary(),controlsBefore=JSON.stringify(replayStart.controls);
  const lateMalformed=JSON.parse(JSON.stringify(cp));lateMalformed.controls={toppleBudget:"invalid"};
  let lateRejected=false;try{lab.applyCheckpoint(lateMalformed);}catch{lateRejected=true;}
  check("late import failure preserves an active avalanche and accounting",lateRejected&&JSON.stringify(lab.snapshot())===activeBefore&&lab.summary()===summaryBefore);
  lab.settle();const activeFuture=stableState();lab.applyCheckpoint(replayStart);lab.addGrains(16,16,300);lab.process(7);lab.settle();
  check("rejected import preserves queued work and deterministic continuation",stableState()===activeFuture&&JSON.stringify(lab.createCheckpoint().controls)===controlsBefore);
  lab.reset({size:16,seed:8,prime:"clear"});lab.setSink(8,8,true);lab.addGrains(8,8,64);check("explicit sink removes incoming grains without accounting drift",lab.snapshot().accountingResidual===0&&lab.snapshot().sinkCount===1);
  const sinkInput=lab.snapshot();
  check("direct sink input is counted as added and immediately dissipated",sinkInput.totalAdded===64&&sinkInput.totalDissipated===64&&sinkInput.mass===0&&sinkInput.currentAvalanche===null);
  check("sink-only input reports dissipation without recording an avalanche",sinkInput.eventCounter===0&&sinkInput.history.length===0&&document.getElementById("dissipated").textContent==="64"&&lab.summary().includes("Dissipated grains: 64; accounting residual: 0"));
  const sinkCheckpoint=lab.createCheckpoint();lab.reset({size:16,seed:1,prime:"clear"});lab.applyCheckpoint(sinkCheckpoint);
  check("checkpoint round-trip retains direct sink accounting",lab.snapshot().totalAdded===64&&lab.snapshot().totalDissipated===64&&lab.snapshot().accountingResidual===0);
  lab.addGrains(7,8,100);lab.process(1);const activeSinkBefore=lab.snapshot();lab.addGrains(8,8,17);const activeSinkAfter=lab.snapshot();
  check("sink input during an avalanche preserves mass and queued work",activeSinkAfter.totalAdded===activeSinkBefore.totalAdded+17&&activeSinkAfter.totalDissipated===activeSinkBefore.totalDissipated+17&&activeSinkAfter.mass===activeSinkBefore.mass&&activeSinkAfter.currentAvalanche.queueRemaining===activeSinkBefore.currentAvalanche.queueRemaining&&activeSinkAfter.currentAvalanche.dissipated===activeSinkBefore.currentAvalanche.dissipated+17&&activeSinkAfter.accountingResidual===0);
  lab.settle();const sinkEvent=lab.snapshot().history.at(-1);
  check("active event records direct sink input in its added and dissipated totals",sinkEvent.grainsAdded===117&&sinkEvent.dissipated>=17&&lab.snapshot().accountingResidual===0);
  return results;
}
