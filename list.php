<?php
// 配置参数
$baseDir = realpath(__DIR__);
$maxZipSize = 100 * 1024 * 1024;
$chunkSize = 8192;

// 安全与工具函数
function sanitizePath($input) {
    return preg_replace('/[^A-Za-z0-9_\-\.\/]/', '', trim($input, '/'));
}

function isPathAllowed($path, $baseDir) {
    $realPath = realpath($path);
    return $realPath !== false && strpos($realPath, $baseDir) === 0;
}

function formatSize($bytes) {
    $units = ['B', 'KB', 'MB', 'GB', 'TB'];
    $i = 0;
    while ($bytes >= 1024 && $i < count($units) - 1) {
        $bytes /= 1024;
        $i++;
    }
    return round($bytes, 2) . ' ' . $units[$i];
}

function generateBreadcrumbs($currentPath, $baseDir) {
    $relativePath = substr($currentPath, strlen($baseDir));
    $parts = explode('/', trim($relativePath, '/'));
    $breadcrumbs = [['name' => '根目录', 'path' => '']];
    $current = '';
    
    foreach ($parts as $part) {
        $current .= '/' . $part;
        $breadcrumbs[] = ['name' => $part, 'path' => ltrim($current, '/')];
    }
    
    return $breadcrumbs;
}

// 文件下载函数（支持断点续传）
function downloadFile($fullPath, $fileName = null) {
    global $chunkSize;
    
    if (!is_file($fullPath)) {
        die("文件不存在");
    }
    
    $fileName = $fileName ?: basename($fullPath);
    $fileSize = filesize($fullPath);
    $mimeType = mime_content_type($fullPath) ?: 'application/octet-stream';
    
    // 处理断点续传
    $range = isset($_SERVER['HTTP_RANGE']) ? $_SERVER['HTTP_RANGE'] : '';
    $start = 0;
    $end = $fileSize - 1;
    
    if (preg_match('/bytes=(\d+)-(\d*)?/', $range, $matches)) {
        $start = intval($matches[1]);
        $end = isset($matches[2]) && $matches[2] ? min(intval($matches[2]), $fileSize - 1) : $end;
        header('HTTP/1.1 206 Partial Content');
        header('Content-Range: bytes ' . $start . '-' . $end . '/' . $fileSize);
    }
    
    $contentLength = $end - $start + 1;
    
    // 设置响应头
    header('Content-Type: ' . $mimeType);
    header('Content-Disposition: attachment; filename="' . $fileName . '"');
    header('Accept-Ranges: bytes');
    header('Content-Length: ' . $contentLength);
    header('Cache-Control: public, must-revalidate');
    header('Pragma: no-cache');
    
    if (ob_get_level()) ob_end_clean();
    
    $handle = fopen($fullPath, 'rb');
    if ($handle) {
        if ($start > 0) fseek($handle, $start);
        
        while (!feof($handle) && $contentLength > 0) {
            $buffer = fread($handle, min($chunkSize, $contentLength));
            echo $buffer;
            $contentLength -= strlen($buffer);
            flush();
        }
        
        fclose($handle);
    }
    
    exit;
}

// 流式ZIP打包下载（无临时文件）
function streamZipDownload($items, $zipName, $baseDir) {
    global $maxZipSize;
    
    // 设置ZIP响应头
    header('Content-Type: application/zip');
    header('Content-Disposition: attachment; filename="' . $zipName . '"');
    header('Cache-Control: no-cache, no-store, must-revalidate');
    header('Pragma: no-cache');
    header('Expires: 0');
    
    if (ob_get_level()) ob_end_clean();
    
    // 创建ZipArchive实例并直接输出到客户端
    $zip = new ZipArchive();
    $zip->open('php://output', ZipArchive::CREATE | ZipArchive::OVERWRITE);
    
    $totalSize = 0;
    $successCount = 0;
    
    foreach ($items as $itemPath) {
        $fullPath = $baseDir . '/' . sanitizePath($itemPath);
        
        if (!isPathAllowed($fullPath, $baseDir)) continue;
        
        if (is_dir($fullPath)) {
            $dirIterator = new RecursiveDirectoryIterator($fullPath, RecursiveDirectoryIterator::SKIP_DOTS);
            $iterator = new RecursiveIteratorIterator($dirIterator);
            
            foreach ($iterator as $file) {
                if (is_file($file)) {
                    $localPath = substr($file, strlen($baseDir) + 1);
                    $zip->addFile($file, $localPath);
                    $successCount++;
                    $totalSize += filesize($file);
                    
                    if ($totalSize > $maxZipSize) {
                        die("打包内容超过最大限制");
                    }
                }
            }
            
            $localDir = substr($fullPath, strlen($baseDir) + 1);
            $zip->addEmptyDir($localDir);
        } elseif (is_file($fullPath)) {
            $localPath = substr($fullPath, strlen($baseDir) + 1);
            $zip->addFile($fullPath, $localPath);
            $successCount++;
            $totalSize += filesize($fullPath);
        }
    }
    
    $zip->close();
    
    if ($successCount === 0) {
        die("没有可打包的有效文件");
    }
    
    exit;
}

// 处理请求
$currentPath = $baseDir;
$relativePath = '';

if (isset($_GET['path'])) {
    $requestedPath = sanitizePath($_GET['path']);
    $currentPath = $baseDir . '/' . $requestedPath;
    
    if (!is_dir($currentPath) || !isPathAllowed($currentPath, $baseDir)) {
        die("非法目录访问");
    }
    
    $relativePath = $requestedPath;
}

// 处理下载请求
if (isset($_GET['download'])) {
    $itemPath = sanitizePath($_GET['download']);
    $fullPath = $baseDir . '/' . $itemPath;
    
    if (!isPathAllowed($fullPath, $baseDir)) {
        die("非法路径访问");
    }
    
    if (is_file($fullPath)) {
        downloadFile($fullPath);
    } elseif (is_dir($fullPath)) {
        $zipName = basename($fullPath) . '_' . date('Ymd_His') . '.zip';
        streamZipDownload([$itemPath], $zipName, $baseDir);
    }
}

// 处理批量打包下载
if (isset($_POST['zipFiles'])) {
    $selectedFiles = isset($_POST['selectedFiles']) ? $_POST['selectedFiles'] : [];
    
    if (empty($selectedFiles)) {
        die("请选择要打包的文件或目录");
    }
    
    $zipName = 'archive_' . date('Ymd_His') . '.zip';
    streamZipDownload($selectedFiles, $zipName, $baseDir);
}

// 获取目录内容
$items = [];
try {
    $dirIterator = new DirectoryIterator($currentPath);
    foreach ($dirIterator as $item) {
        if ($item->isDot()) continue;
        
        $itemPath = $item->getPathname();
        if (!isPathAllowed($itemPath, $baseDir)) continue;
        
        $items[] = [
            'name' => $item->getFilename(),
            'isDir' => $item->isDir(),
            'size' => $item->isDir() ? 0 : $item->getSize(),
            'size_human' => $item->isDir() ? '-' : formatSize($item->getSize()),
            'modified' => $item->getMTime(),
            'modified_human' => date('Y-m-d H:i:s', $item->getMTime()),
            'path' => substr($itemPath, strlen($baseDir) + 1),
            'extension' => $item->isDir() ? '' : strtolower(pathinfo($item->getFilename(), PATHINFO_EXTENSION)),
            'checked' => false
        ];
    }
} catch (Exception $e) {
    die("无法访问目录: " . $e->getMessage());
}

$fileDataJson = json_encode($items);
$breadcrumbs = generateBreadcrumbs($currentPath, $baseDir);
?>

<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>文件管理器</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; max-width: 1200px; margin: 0 auto; padding: 20px; }
        .container { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .error { color: #dc3545; background: #f8d7da; padding: 10px; border-radius: 4px; margin-bottom: 15px; }
        .toolbar { display: flex; gap: 10px; margin-bottom: 20px; flex-wrap: wrap; }
        .search-input, .action-button { padding: 10px 15px; border-radius: 4px; font-size: 1rem; }
        .search-input { flex: 1; border: 1px solid #ced4da; }
        .action-button { background: #007bff; color: white; border: none; cursor: pointer; min-width: 100px; }
        .action-button:hover { background: #0069d9; }
        .breadcrumb { margin-bottom: 20px; font-size: 0.9rem; color: #6c757d; }
        .breadcrumb a { color: #007bff; text-decoration: none; }
        .breadcrumb a:hover { text-decoration: underline; }
        .file-table { width: 100%; border-collapse: collapse; }
        .file-table th, .file-table td { border-bottom: 1px solid #dee2e6; text-align: left;  padding: 3px; }
        .file-table th {background: #e9ecef; color: #495057; font-weight: 600; cursor: pointer; }
        .sort-icon { margin-left: 5px; color: #6c757d; }
        .folder-row { color: #007bff; font-weight: 600; }
        .folder-link { color: #007bff; text-decoration: none; }
        .folder-link:hover { text-decoration: underline; }
        .download-link { color: #007bff; padding: 3px; }
        .download-link:hover { background: #e9ecef; }
        .no-files { text-align: center; padding: 20px; color: #6c757d; }
        @media (max-width: 768px) {
            .toolbar { flex-direction: column; }
            .file-table th, .file-table td { padding: 8px; font-size: 0.9rem; }
            .breadcrumb { font-size: 0.8rem; }
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>文件管理器</h1>
        <div class="breadcrumb"><?php foreach ($breadcrumbs as $i => $crumb): ?>
            <?php if ($i < count($breadcrumbs) - 1): ?>
                <a href="?path=<?php echo urlencode($crumb['path']); ?>"><?php echo htmlspecialchars($crumb['name']); ?></a> &gt;
            <?php else: ?>
                <span><?php echo htmlspecialchars($crumb['name']); ?></span>
            <?php endif; ?>
        <?php endforeach; ?></div>
        
        <form id="fileForm" method="post">
            <div class="toolbar">
                <input type="text" id="search-input" class="search-input" placeholder="搜索文件或文件夹...">
                <button type="button" id="select-all" class="action-button">全选</button>
                <button type="button" id="deselect-all" class="action-button">取消全选</button>
                <button type="submit" name="zipFiles" class="action-button">打包下载所选</button>
            </div>
            
            <table class="file-table">
                <thead>
                    <tr>
                        <th><input type="checkbox" id="header-checkbox"></th>
                        <th data-sort="name">名称 <span class="sort-icon">↑</span></th>
                        <th data-sort="extension">扩展名 <span class="sort-icon"></span></th>
                        <th data-sort="size">大小 <span class="sort-icon"></span></th>
                        <th data-sort="modified">修改时间 <span class="sort-icon"></span></th>
                        <th>操作</th>
                    </tr>
                </thead>
                <tbody id="file-list"></tbody>
            </table>
        </form>
    </div>
    
    <script>
        const fileData = <?php echo $fileDataJson; ?>;
        let filteredData = [...fileData];
        let sortField = 'name';
        let sortAsc = true;
        
        function debounce(func, delay) {
            let timer;
            return function() {
                const context = this;
                const args = arguments;
                clearTimeout(timer);
                timer = setTimeout(() => func.apply(context, args), delay);
            };
        }
        
        function renderFileList() {
            const tbody = document.getElementById('file-list');
            tbody.innerHTML = filteredData.length ? '' : 
                '<tr><td colspan="6" class="no-files">没有找到匹配的文件或目录</td></tr>';
            
            filteredData.forEach(item => {
                const row = document.createElement('tr');
                row.className = item.isDir ? 'folder-row' : '';
                row.innerHTML = `
                    <td><input type="checkbox" name="selectedFiles[]" value="${item.path}" ${item.checked ? 'checked' : ''}></td>
                    <td>${item.isDir ? 
                        `<a href="?path=${encodeURIComponent(item.path)}" class="folder-link">${item.name}/</a>` : 
                        item.name}</td>
                    <td>${item.extension ? '.' + item.extension : '-'}</td>
                    <td>${item.size_human}</td>
                    <td>${item.modified_human}</td>
                    <td><a href="${item.path}" class="download-link">打开</a>&nbsp;&nbsp;<a href="?download=${encodeURIComponent(item.path)}" class="download-link">下载</a></td>
                `;
                tbody.appendChild(row);
            });
            
            updateHeaderCheckbox();
            
            document.querySelectorAll('input[name="selectedFiles[]"]').forEach(checkbox => {
                checkbox.addEventListener('change', function() {
                    fileData.find(item => item.path === this.value).checked = this.checked;
                    updateHeaderCheckbox();
                });
            });
        }
        
        function updateHeaderCheckbox() {
            const checkboxes = document.querySelectorAll('input[name="selectedFiles[]"]');
            const checkedCount = document.querySelectorAll('input[name="selectedFiles[]"]:checked').length;
            const headerCheckbox = document.getElementById('header-checkbox');
            
            headerCheckbox.checked = checkboxes.length > 0 && checkedCount === checkboxes.length;
            headerCheckbox.indeterminate = checkboxes.length > 0 && checkedCount > 0 && checkedCount < checkboxes.length;
        }
        
        document.getElementById('search-input').addEventListener('input', debounce(function() {
            const searchTerm = this.value.toLowerCase().trim();
            filteredData = searchTerm ? 
                fileData.filter(item => 
                    item.name.toLowerCase().includes(searchTerm) || 
                    (item.extension && item.extension.toLowerCase().includes(searchTerm))
                ) : [...fileData];
            
            filteredData.sort((a, b) => a.isDir === b.isDir ? 0 : (a.isDir ? -1 : 1));
            renderFileList();
        }, 200));
        
        document.querySelectorAll('th[data-sort]').forEach(header => {
            header.addEventListener('click', function() {
                const field = this.dataset.sort;
                sortAsc = field === sortField ? !sortAsc : true;
                sortField = field;
                
                document.querySelectorAll('th[data-sort] .sort-icon').forEach(icon => icon.textContent = '');
                this.querySelector('.sort-icon').textContent = sortAsc ? '↑' : '↓';
                
                filteredData.sort((a, b) => {
                    if (a.isDir !== b.isDir) return a.isDir ? -1 : 1;
                    
                    let valueA, valueB;
                    switch (sortField) {
                        case 'name': [valueA, valueB] = [a.name, b.name]; break;
                        case 'extension': [valueA, valueB] = [a.extension || '', b.extension || '']; break;
                        case 'size': [valueA, valueB] = [a.size, b.size]; break;
                        case 'modified': [valueA, valueB] = [a.modified, b.modified]; break;
                        default: return 0;
                    }
                    
                    return sortAsc ? 
                        (typeof valueA === 'string' ? valueA.localeCompare(valueB) : valueA - valueB) : 
                        (typeof valueA === 'string' ? valueB.localeCompare(valueA) : valueB - valueA);
                });
                
                renderFileList();
            });
        });
        
        document.getElementById('select-all').addEventListener('click', () => {
            document.querySelectorAll('input[name="selectedFiles[]"]').forEach(checkbox => {
                checkbox.checked = true;
                fileData.find(item => item.path === checkbox.value).checked = true;
            });
            updateHeaderCheckbox();
        });
        
        document.getElementById('deselect-all').addEventListener('click', () => {
            document.querySelectorAll('input[name="selectedFiles[]"]').forEach(checkbox => {
                checkbox.checked = false;
                fileData.find(item => item.path === checkbox.value).checked = false;
            });
            updateHeaderCheckbox();
        });
        
        document.getElementById('header-checkbox').addEventListener('change', function() {
            document.querySelectorAll('input[name="selectedFiles[]"]').forEach(checkbox => {
                checkbox.checked = this.checked;
                fileData.find(item => item.path === checkbox.value).checked = this.checked;
            });
            updateHeaderCheckbox();
        });
        
        filteredData.sort((a, b) => a.isDir === b.isDir ? 0 : (a.isDir ? -1 : 1));
        renderFileList();
    </script>
</body>
</html>