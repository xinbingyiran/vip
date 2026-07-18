// 捕获阶段拦截所有点击，彻底屏蔽document所有click逻辑
document.body.addEventListener('click', function(e) {
  e.stopImmediatePropagation(); // 阻止同元素剩余同类型监听执行
  e.preventDefault();
}, true); // true = 捕获阶段优先执行

// 清除页面所有元素上的click监听（包含document自身）
function removeAllClickListeners() {
  const allNodes = document.querySelectorAll('*');
  allNodes.forEach(node => {
    const clone = node.cloneNode(true);
    node.parentNode.replaceChild(clone, node);
  });
}
removeAllClickListeners();


document.body.addEventListener('click',e=>(e.stopImmediatePropagation(),e.preventDefault()),true);document.querySelectorAll('*').forEach(n=>n.parentNode.replaceChild(n.cloneNode(1),n));