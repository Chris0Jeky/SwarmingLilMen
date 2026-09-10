() => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const lab=window.Ecogenesis;lab.pause();
  const fresh=seed=>{lab.reset({preset:"genesis",seed,paused:true});lab.run(60);return lab.hash();};
  const a=fresh(424242),b=fresh(424242),c=fresh(171717);check("same seed reproduces full-state fingerprint",a===b);check("different seeds diverge",a!==c);
  const cp=lab.checkpoint(),hash=lab.hash();lab.run(60);const future=lab.hash();lab.restore(cp);check("checkpoint restores numeric state",lab.hash()===hash);lab.run(60);check("restored RNG and recurrent state reproduce future",lab.hash()===future);
  const invalid=lab.checkpoint();invalid.agents[0].g.pop();const prior=lab.hash();let rejected=false;try{lab.restore(invalid);}catch{rejected=true;}check("malformed genome rejected before live mutation",rejected&&prior===lab.hash());
  lab.reset({preset:"cambrian",seed:334455,paused:true});lab.run(900);const m=lab.snapshot();check("bounded endurance run remains finite",lab.finite());check("reproduction actually executes",m.counters.births>0&&m.maxGeneration>0,{births:m.counters.births,generation:m.maxGeneration});check("population remains inside configured ceiling",m.population<=m.parameters.populationCap);
  for(const shock of ["bloom","drought","winter","plague","mutation","meteor"]){lab.shock(shock);lab.run(4);check("intervention "+shock+" stays finite",lab.finite());}
  return results;
}
