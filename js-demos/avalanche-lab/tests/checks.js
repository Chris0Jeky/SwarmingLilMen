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
  lab.reset({size:16,seed:8,prime:"clear"});lab.setSink(8,8,true);lab.addGrains(8,8,64);check("explicit sink removes incoming grains without accounting drift",lab.snapshot().accountingResidual===0&&lab.snapshot().sinkCount===1);
  return results;
}
