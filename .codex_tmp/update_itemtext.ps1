function Update-DetailJson($path, $insertBlock) {
  $full = Join-Path (Get-Location) $path
  $content = [System.IO.File]::ReadAllText($full, [System.Text.Encoding]::UTF8)
  if ($content -match '"key"\s*:\s*"amountLabel"') {
    return
  }

  $pattern = '(?ms)(\s*\{\s*"key"\s*:\s*"priceFormat",\s*"value"\s*:\s*".*?"\s*\})'
  $updated = [System.Text.RegularExpressions.Regex]::Replace($content, $pattern, ',' + [Environment]::NewLine + $insertBlock, 1)
  [System.IO.File]::WriteAllText($full, $updated, [System.Text.UTF8Encoding]::new($false))
}

Update-DetailJson 'Assets/Resources/Localization/en/ItemTextDatabase.json' @'
        {
          "key": "amountLabel",
          "value": "Amount"
        },
        {
          "key": "priceLabel",
          "value": "Value"
        },
        {
          "key": "ownerLabel",
          "value": "Owner"
        },
        {
          "key": "descriptionTitle",
          "value": "Description"
        }
'@

Update-DetailJson 'Assets/Resources/Localization/vi/ItemTextDatabase.json' @'
        {
          "key": "amountLabel",
          "value": "Số lượng"
        },
        {
          "key": "priceLabel",
          "value": "Giá trị"
        },
        {
          "key": "ownerLabel",
          "value": "Chủ sở hữu"
        },
        {
          "key": "descriptionTitle",
          "value": "Mô tả"
        }
'@

Update-DetailJson 'Assets/Resources/Localization/zh/ItemTextDatabase.json' @'
        {
          "key": "amountLabel",
          "value": "数量"
        },
        {
          "key": "priceLabel",
          "value": "价值"
        },
        {
          "key": "ownerLabel",
          "value": "持有者"
        },
        {
          "key": "descriptionTitle",
          "value": "描述"
        }
'@
