-- 1. Tạo Schema
CREATE SCHEMA IF NOT EXISTS ocr_clos;

-- 2. Tạo Bảng sftpocrfile
CREATE TABLE ocr_clos.sftpocrfile (
    id BIGSERIAL PRIMARY KEY,
    filename TEXT,
    statusocr INT DEFAULT 0,
    pathinput TEXT,
    pathoutput TEXT,
    created TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    uploadtime TIMESTAMP WITH TIME ZONE,
    statusupdate INT DEFAULT 0,
    circular TEXT,
    dip TEXT,
    method TEXT,
    typeocr TEXT,
    dateretry TIMESTAMP WITH TIME ZONE,
    countretry INT DEFAULT 0,
    pagecount INT DEFAULT 0,
    statuscheck INT DEFAULT 0,
    timeocr TEXT,
    status INT DEFAULT 0,
    accuracyrate DOUBLE PRECISION DEFAULT 0
);

-- 3. Insert vài dòng dữ liệu để Project có cái hiển thị
INSERT INTO ocr_clos.sftpocrfile (filename, statusocr, pathinput, pagecount, accuracyrate, status)
VALUES 
('invoice_sample_01.pdf', 1, '/data/input/', 5, 98.5, 1),
('contract_002.pdf', 0, '/data/input/', 12, 0, 0);



-- Xóa dữ liệu cũ nếu muốn làm sạch
-- TRUNCATE TABLE ocr_clos.sftpocrfile;

INSERT INTO ocr_clos.sftpocrfile 
(filename, statusocr, pathinput, created, uploadtime, pagecount, accuracyrate, timeocr, circular, status)
VALUES 
-- Case 1: Thành công, tốc độ nhanh (10s/5 trang = 2s/trang), chính xác cao
('invoice_fast_001.pdf', 1, '/in/1.pdf', NOW() - INTERVAL '1 hour', NOW() - INTERVAL '50 minutes', 5, 99.2, '00:00:10', NULL, 1),

-- Case 2: Thành công, tốc độ chậm (60s/2 trang = 30s/trang), chính xác vừa phải
('contract_slow_002.pdf', 1, '/in/2.pdf', NOW() - INTERVAL '2 hours', NOW() - INTERVAL '110 minutes', 2, 85.5, '00:01:00', NULL, 1),

-- Case 3: OCR lỗi (Giả sử statusocr = 2 hoặc 3 là lỗi dựa trên GetFailedStatusValues)
('error_file_003.pdf', 2, '/in/3.pdf', NOW() - INTERVAL '1 day', NOW() - INTERVAL '23 hours', 10, 0, NULL, NULL, 0),

-- Case 4: Xử lý thủ công (Có thông tư - circular IS NOT NULL)
('manual_doc_004.pdf', 1, '/in/4.pdf', NOW() - INTERVAL '5 hours', NOW() - INTERVAL '4 hours', 8, 95.0, '00:00:40', 'TT-01/2024', 1),

-- Case 5: Dữ liệu từ ngày hôm qua
('yesterday_doc_005.pdf', 1, '/in/5.pdf', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', 15, 97.8, '00:02:30', NULL, 1),

-- Case 6 -> 10: Thêm một số bản ghi ngẫu nhiên để biểu đồ có độ dốc
('random_006.pdf', 1, '/in/6.pdf', NOW(), NOW(), 3, 91.0, '00:00:15', NULL, 1),
('random_007.pdf', 3, '/in/7.pdf', NOW(), NOW(), 1, 0, NULL, NULL, 0),
('random_008.pdf', 1, '/in/8.pdf', NOW(), NOW(), 20, 96.5, '00:03:00', 'TT-02', 1),
('random_009.pdf', 1, '/in/9.pdf', NOW(), NOW(), 4, 94.2, '00:00:20', NULL, 1),
('random_010.pdf', 0, '/in/10.pdf', NOW(), NOW(), 2, 0, NULL, NULL, 0); -- Đang chờ xử lý
