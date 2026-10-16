() => {
  const results=[];
  const check=(name,condition,detail=null)=>{if(!condition)throw new Error(name);results.push({name,status:"passed",detail});};
  const lab=window.Ecogenesis;lab.pause();
  const fresh=seed=>{lab.reset({preset:"genesis",seed,paused:true});lab.run(60);return lab.hash();};
  const a=fresh(424242),b=fresh(424242),c=fresh(171717);check("same seed reproduces full-state fingerprint",a===b);check("different seeds diverge",a!==c);
  const cp=lab.checkpoint(),hash=lab.hash();lab.run(60);const future=lab.hash();lab.restore(cp);check("checkpoint restores numeric state",lab.hash()===hash);lab.run(60);check("restored RNG and recurrent state reproduce future",lab.hash()===future);
  const invalid=lab.checkpoint();invalid.agents[0].g.pop();const prior=lab.hash();let rejected=false;try{lab.restore(invalid);}catch{rejected=true;}check("malformed genome rejected before live mutation",rejected&&prior===lab.hash());
  const incomplete=lab.checkpoint();delete incomplete.params.plantGrowth;rejected=false;try{lab.restore(incomplete);}catch{rejected=true;}check("incomplete runtime parameters are rejected before live mutation",rejected&&prior===lab.hash());
  const hostile=lab.checkpoint();hostile.eventLog.unshift({tick:0,title:"<img src=x onerror=window.__checkpointXss=true>",text:"<script>window.__checkpointXss=true</script>",type:"info"});lab.restore(hostile);check("checkpoint event text is rendered without HTML execution",!window.__checkpointXss&&!document.querySelector("#eventLog img")&&document.querySelector("#eventLog").textContent.includes("<img src=x"));
  const hostileArchive=lab.checkpoint(),selected=hostileArchive.agents[0].id;
  hostileArchive.selectedId=selected;hostileArchive.archive=[[selected,{id:selected,parentA:0,parentB:0,birthTick:0,deathTick:1,generation:0,speciesId:"<img src=x onerror=window.__checkpointXss=true>",cause:"<script>window.__checkpointXss=true</script>",traits:[0,0,0,0]}]];
  const beforeArchive=JSON.stringify(lab.checkpoint());rejected=false;try{lab.restore(hostileArchive);}catch{rejected=true;}
  check("hostile archive import is rejected without creating executable elements",rejected&&!window.__checkpointXss&&!document.querySelector("#ancestryList img, #ancestryList script, #ancestryList [onerror]")&&JSON.stringify(lab.checkpoint())===beforeArchive);
  const textArchive=lab.checkpoint();textArchive.selectedId=textArchive.agents[0].id;const record=textArchive.archive.find(([id])=>id===textArchive.selectedId)[1];record.deathTick=1;record.cause="<img src=x onerror=window.__checkpointXss=true><script>bad()</script>";lab.restore(textArchive);
  check("ancestry cause is displayed literally without HTML elements",document.querySelector("#ancestryList").textContent.includes(record.cause)&&!document.querySelector("#ancestryList img, #ancestryList script, #ancestryList [onerror]"));
  const valid=JSON.stringify(lab.checkpoint());
  const invalidImports=[
    ["archive tuple",cp=>cp.archive[0]=[1]],
    ["archive duplicate",cp=>cp.archive.push(cp.archive[0])],
    ["archive key",cp=>cp.archive[0][0]="1"],
    ["archive id",cp=>cp.archive[0][1].id=-1],
    ["archive species",cp=>cp.archive[0][1].speciesId="<script>bad()</script>"],
    ...["parentA","parentB","birthTick","deathTick","generation"].map(key=>["archive "+key,cp=>cp.archive[0][1][key]=1.5]),
    ["archive cause type",cp=>cp.archive[0][1].cause={}],
    ["archive cause bound",cp=>cp.archive[0][1].cause="x".repeat(257)],
    ["archive traits length",cp=>cp.archive[0][1].traits.pop()],
    ["archive traits numeric",cp=>cp.archive[0][1].traits[0]="0"],
    ["archive traits finite",cp=>cp.archive[0][1].traits[0]=Infinity],
    ["archive bound",cp=>cp.archive=Array.from({length:12001},(_,i)=>{const id=cp.nextId+i;return[id,{...cp.archive[0][1],id}];})],
    ["history bound",cp=>cp.history=Array(261).fill(cp.history[0])],
    ["history numeric",cp=>cp.history[0].total="<img src=x>"],
    ["species bound",cp=>{const first=cp.species[0];cp.species=Array.from({length:12001},(_,i)=>({...first,id:i+1}));cp.nextSpeciesId=12002;}],
    ["species signature bound",cp=>cp.species[0].signature.push(0)],
    ["selected id type",cp=>cp.selectedId="1"],
    ["selected id missing",cp=>cp.selectedId=cp.nextId],
    ["Float32 environment overflow",cp=>{cp.seed++;cp.env.plants[0]=1e100;}],
    ["Float32 memory overflow",cp=>{cp.seed++;cp.agents[0].memory[0]=1e100;}]
  ];
  for(const [name,mutate] of invalidImports){const bad=JSON.parse(valid);mutate(bad);rejected=false;try{lab.restore(bad);}catch{rejected=true;}check(name+" rejected without live mutation",rejected&&JSON.stringify(lab.checkpoint())===valid);}
  const extra=JSON.parse(valid);extra.agents[0]={...extra.agents[0],...JSON.parse('{"name":"bad","__proto__":{"trophic":"bad"},"unexpected":true}')};lab.restore(extra);const restored=lab.select(extra.agents[0].id);
  check("organism restore ignores unrecognized and prototype keys",typeof restored.name==="function"&&typeof restored.trophic==="function"&&!Object.hasOwn(restored,"unexpected")&&!Object.hasOwn(restored,"__proto__"));
  const unselected=lab.checkpoint();unselected.selectedId=null;lab.restore(unselected);check("null selection round trips",lab.checkpoint().selectedId===null);
  const legacyZero=lab.checkpoint();legacyZero.selectedId=0;let zeroOk=true;try{lab.restore(legacyZero);}catch{zeroOk=false;}check("legacy selectedId 0 imports as no selection",zeroOk&&lab.checkpoint().selectedId===null);
  lab.reset({preset:"cambrian",seed:334455,paused:true});lab.run(900);const m=lab.snapshot();check("bounded endurance run remains finite",lab.finite());check("reproduction actually executes",m.counters.births>0&&m.maxGeneration>0,{births:m.counters.births,generation:m.maxGeneration});check("population remains inside configured ceiling",m.population<=m.parameters.populationCap);
  for(const shock of ["bloom","drought","winter","plague","mutation","meteor"]){lab.shock(shock);lab.run(4);check("intervention "+shock+" stays finite",lab.finite());}
  return results;
}
