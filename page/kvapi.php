<?php
header("Content-Type: application/json; charset=utf-8");
header("Access-Control-Allow-Origin: *");
header("Access-Control-Allow-Methods: POST");
header("Access-Control-Allow-Headers: Content-Type");
ini_set('display_errors', 0);
error_reporting(0);

try {
    $db = new SQLite3('kv.db');
    $db->busyTimeout(5000);
    $db->exec("CREATE TABLE IF NOT EXISTS kv (
        type TEXT NOT NULL,
        name TEXT NOT NULL,
        value TEXT,
        PRIMARY KEY (type, name)
    )");

    function ret($code = 0, $msg = 'success', $data = []) {
        echo json_encode(['code' => $code, 'msg' => $msg, 'data' => $data], JSON_UNESCAPED_UNICODE);
        exit;
    }

    $input = file_get_contents('php://input');
    $p = json_decode($input, true) ?: [];
    $action = strtolower(trim($p['action'] ?? ''));
    $data   = $p['data'] ?? [];

    // ==========================
    // ✔ 100% 按你要求写的 toList
    // ==========================
    function toList($d) {
        if (!is_array($d)) return [];
        if (array_keys($d) !== array_keys(array_keys($d))) return [$d];
        return $d;
    }

    // ==========================
    // 查询
    // ==========================
    if ($action === 'get') {
        $list = toList($data);
        $where = [];
        $params = [];

        foreach ($list as $item) {
            $t = $item['type'] ?? '';
            $n = $item['name'] ?? '';
            $w = [];
            if ($t !== '') { $w[] = "type=?"; $params[] = $t; }
            if ($n !== '') { $w[] = "name=?"; $params[] = $n; }
            if (!empty($w)) $where[] = "(".implode(" AND ", $w).")";
        }

        $sql = "SELECT type,name,value FROM kv";
        if (!empty($where)) $sql .= " WHERE " . implode(" OR ", $where);

        $stmt = $db->prepare($sql);
        foreach ($params as $i => $v) $stmt->bindValue($i+1, $v);
        $res = $stmt->execute();
        $result = [];
        while ($row = $res->fetchArray(SQLITE3_ASSOC)) $result[] = $row;
        ret(0, 'success', $result);
    }

    // ==========================
    // 删除
    // ==========================
    if ($action === 'delete') {
        $list = toList($data);
        $where = [];
        $params = [];

        foreach ($list as $item) {
            $t = $item['type'] ?? '';
            $n = $item['name'] ?? '';
            $w = [];
            if ($t !== '') { $w[] = "type=?"; $params[] = $t; }
            if ($n !== '') { $w[] = "name=?"; $params[] = $n; }
            if (!empty($w)) $where[] = "(".implode(" AND ", $w).")";
        }

        $sql = "DELETE FROM kv";
        if (!empty($where)) $sql .= " WHERE " . implode(" OR ", $where);

        $stmt = $db->prepare($sql);
        foreach ($params as $i => $v) $stmt->bindValue($i+1, $v);
        $stmt->execute();
        ret(0, 'delete_success', ['rows' => $db->changes()]);
    }

    // ==========================
    // 保存
    // ==========================
    if (in_array($action, ['save','insert','update','add'])) {
        $list = toList($data);
        $valid = [];
        foreach ($list as $item) {
            $t = trim($item['type'] ?? '');
            $n = trim($item['name'] ?? '');
            $v = $item['value'] ?? '';
            if ($t !== '' && $n !== '') $valid[] = [$t, $n, $v];
        }

        if (empty($valid)) ret(1, 'no valid data');

        $places = implode(',', array_fill(0, count($valid), '(?,?,?)'));
        $params = [];
        foreach ($valid as $i) {
            $params[] = $i[0];
            $params[] = $i[1];
            $params[] = $i[2];
        }

        $sql = "INSERT OR REPLACE INTO kv (type,name,value) VALUES $places";
        $stmt = $db->prepare($sql);
        foreach ($params as $i => $v) $stmt->bindValue($i+1, $v);
        $stmt->execute();

        ret(0, 'save_success', ['rows' => $db->changes()]);
    }

    ret(1, 'action not supported');

} catch (Throwable $e) {
    ret(-1, 'error: ' . $e->getMessage());
}
?>