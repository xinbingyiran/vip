<?php
error_reporting(0);
header('Content-Type: text/html; charset=utf-8');

$action   = $_POST['action'] ?? '';
$dbfile   = trim($_POST['dbfile'] ?? 'data.db');
$sql      = trim($_POST['sql'] ?? '');
$driver   = $_POST['driver'] ?? 'pdo';
$results  = [];
$msg      = '';
$error    = '';

$dbfile = preg_replace('/[^a-z0-9_\.\/]/i', '', $dbfile);
if (empty($dbfile)) $dbfile = 'data.db';
if (!str_ends_with($dbfile, '.db')) $dbfile .= '.db';

$dir = dirname($dbfile);
@mkdir($dir, 0755, true);

if ($action === 'delete') {
    if (file_exists($dbfile)) {
        unlink($dbfile);
        $msg = "✅ 删除成功：{$dbfile}";
        $dbfile = 'data.db';
    } else {
        $error = "❌ 文件不存在";
    }
}

if ($action === 'run' && $sql) {
    $sqlList = [];
    $buf = '';
    $inString = false;
    $len = strlen($sql);
    for ($i = 0; $i < $len; $i++) {
        $c = $sql[$i];
        if ($c === "'") $inString = !$inString;
        if ($c === ';' && !$inString) {
            $buf = trim($buf);
            if (!empty($buf)) $sqlList[] = $buf;
            $buf = '';
        } else {
            $buf .= $c;
        }
    }
    $buf = trim($buf);
    if (!empty($buf)) $sqlList[] = $buf;

    try {
        if ($driver === 'pdo') {
            $pdo = new PDO("sqlite:$dbfile");
            $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
            foreach ($sqlList as $q) {
                $cleanSql = preg_replace('/--.*?$|\/\*[\s\S]*?\*\//m', '', $q);
                $cleanSql = trim($cleanSql);
                if (preg_match('/^\s*(select|pragma|with)/i', $cleanSql)) {
                    $stmt = $pdo->query($q);
                    $data = $stmt ? $stmt->fetchAll(PDO::FETCH_ASSOC) : [];
                    $results[] = ['sql' => $q, 'type' => 'query', 'data' => $data];
                } else {
                    $affected = $pdo->exec($q);
                    $results[] = ['sql' => $q, 'type' => 'exec', 'rows' => $affected];
                }
            }
        } else {
            $db = new SQLite3($dbfile);
            $db->enableExceptions(true);
            foreach ($sqlList as $q) {
                $cleanSql = preg_replace('/--.*?$|\/\*[\s\S]*?\*\//m', '', $q);
                $cleanSql = trim($cleanSql);
                if (preg_match('/^\s*(select|pragma|with)/i', $cleanSql)) {
                    $stmt = $db->query($q);
                    $data = [];
                    while ($row = $stmt->fetchArray(SQLITE3_ASSOC)) $data[] = $row;
                    $results[] = ['sql' => $q, 'type' => 'query', 'data' => $data];
                } else {
                    $db->exec($q);
                    $affected = $db->changes();
                    $results[] = ['sql' => $q, 'type' => 'exec', 'rows' => $affected];
                }
            }
            $db->close();
        }
    } catch (Exception $e) {
        $error = "❌ 错误：" . $e->getMessage();
    }
}
?>
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<title>SQLite 管理工具</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{background:#f5f7fa;padding:20px;font-size:14px}
.container{max-width:1000px;margin:auto;background:#fff;border-radius:10px;padding:20px}
h1{margin-bottom:15px}
input,textarea,button,select{width:100%;padding:10px;border:1px solid #ddd;border-radius:6px;margin-bottom:10px}
textarea{height:200px;font-family:monospace}
button{cursor:pointer}
.btn-run{background:#007bff;color:white;border:none}
.btn-del{background:#dc3545;color:white;border:none}
.msg{padding:10px;border-radius:6px;margin:10px 0}
.success{background:#e8f8f0;color:#0a5}
.danger{background:#f8d7da;color:#dc3545}
.sql-item{margin:12px 0;padding:10px;background:#f9f9f9;border-radius:6px}
.sql-text{font-weight:bold;margin-bottom:5px}
pre{font-size:12px;background:#fff;padding:5px;border-radius:4px;white-space:pre-wrap}
.table-box{overflow-x:auto;margin-top:8px}
table{width:100%;border-collapse:collapse}
table,th,td{border:1px solid #ddd}
th,td{padding:6px}
th{background:#f8f9fa}
.driver-box{display:flex;gap:10px}
</style>
</head>
<body>
<div class="container">
<h1>SQLite 管理工具</h1>

<form method="post">
    <input type="text" name="dbfile" value="<?=htmlspecialchars($dbfile)?>" placeholder="数据库路径">
    <div class="driver-box">
        <select name="driver">
            <option value="pdo" <?= $driver=='pdo'?'selected':'' ?>>PDO</option>
            <option value="sqlite3" <?= $driver=='sqlite3'?'selected':'' ?>>SQLite3</option>
        </select>
    </div>
    <textarea name="sql" placeholder="支持：注释、跨行、多语句、PRAGMA"><?=htmlspecialchars($sql)?></textarea>
    <button type="submit" name="action" value="run" class="btn-run">执行</button>
    <button type="submit" name="action" value="delete" class="btn-del" onclick="return confirm('确定删除？')">删除库</button>
</form>

<?php if($msg): ?>
<div class="success msg"><?=$msg?></div>
<?php endif; ?>

<?php if($error): ?>
<div class="danger msg"><?=$error?></div>
<?php endif; ?>

<?php foreach($results as $i=>$item): ?>
<div class="sql-item">
    <div class="sql-text">语句 <?= $i+1 ?></div>
    <pre><?=htmlspecialchars($item['sql'])?></pre>
    <?php if($item['type']=='exec'): ?>
        <div class="success">✅ 受影响行数：<?=$item['rows']?></div>
    <?php else: ?>
        <div class="table-box">
            <table>
                <?php if($item['data']): ?>
                <tr>
                    <?php foreach(array_keys($item['data'][0]) as $col): ?>
                    <th><?=$col?></th>
                    <?php endforeach; ?>
                </tr>
                <?php foreach($item['data'] as $row): ?>
                <tr>
                    <?php foreach($row as $v): ?>
                    <td><?=htmlspecialchars($v)?></td>
                    <?php endforeach; ?>
                </tr>
                <?php endforeach; ?>
                <?php else: ?>
                <tr><td>无结果</td></tr>
                <?php endif; ?>
            </table>
        </div>
    <?php endif; ?>
</div>
<?php endforeach; ?>
</div>
</body>
</html>