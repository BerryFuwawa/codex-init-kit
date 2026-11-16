let i=["manifest.json","bin/node.exe","bin/node_repl.exe"].map(e=>L({destinationPath:r,executableName:e,sourcePath:(0,o.join)(t,e)})),a=M(i).slice(0,16);
function L({destinationPath:e,executableName:t,sourcePath:r}){return{digest:D({destinationPath:e,executableName:t,filePath:r,operation:"hash_source",sourcePath:r}),executableName:t,size:1,sourcePath:r}}
function M(e){let t=(0,n.createHash)("sha256");for(let r of e)t.update(r.executableName),t.update("\0"),t.update(r.digest),t.update("\0");return t.digest("hex")}
function D({destinationPath:e,executableName:t,operation:r,sourcePath:s,filePath:i}){return W({destinationPath:e,executableName:t,operation:r,sourcePath:s},()=>{let e=(0,n.createHash)("sha256");return j(i,t=>{e.update(t)}),e.digest("hex")})}
function j(e,t){let r=(0,s.openSync)(e,"r");try{let e=(0,s.fstatSync)(r).size,n=Buffer.allocUnsafe(Math.min(Math.max(1,e),262144));for(;;){let e=(0,s.readSync)(r,n);if(0===e)return;t(n.subarray(0,e))}}finally{(0,s.closeSync)(r)}}
