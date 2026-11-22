// Clipboard HTML samples for the paste tests (E05-02, #37): tests/editor/inline.spec.ts and e2e/editor.spec.ts.

// What Word puts on the clipboard (shortened): a whole document with styles, conditional comments, Office elements,
// classes and inline styles, bold and italic by tags and by styles, a highlight, an empty paragraph.
export const wordHtml = `<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word">
<head><meta charset="utf-8"><title>Документ</title>
<!--[if gte mso 9]><xml><w:WordDocument><w:View>Normal</w:View></w:WordDocument></xml><![endif]-->
<style><!-- p.MsoNormal {margin:0cm; font-size:11.0pt;} --></style></head>
<body lang=RU style='tab-interval:35.4pt'><!--StartFragment-->
<p class=MsoNormal style='margin-bottom:0cm'><b><span style='font-size:14.0pt;mso-bidi-font-weight:normal'>Заголовок
отчёта</span></b><o:p></o:p></p>
<p class=MsoNormal><span style='font-family:"Calibri",sans-serif'>Обычный текст, <i>курсив</i> и
<span style='font-weight:bold'>жирный стилем</span>, <span style='background:yellow;mso-highlight:yellow'>выделение</span>.</span></p>
<p class=MsoNormal><o:p>&nbsp;</o:p></p>
<p class=MsoNormal>Ссылка <a href="https://example.test/report?id=1&amp;x=2" target="_blank" class=MsoHyperlink>на отчёт</a>
и <a href="javascript:alert(1)">опасная</a>.<img src="file:///C:/image001.png"></p>
<!--EndFragment--></body></html>`

// What a browser puts on the clipboard from Google Docs: everything in <b style="font-weight:normal">, formatting by
// spans with styles.
export const googleDocsHtml = `<meta charset="utf-8"><b style="font-weight:normal;" id="docs-internal-guid-1234"><p dir="ltr" style="line-height:1.38;margin-top:0pt;"><span style="font-size:11pt;font-weight:400;">Простой текст и </span><span style="font-size:11pt;font-weight:700;">жирный</span><span style="font-size:11pt;font-style:italic;"> курсив</span></p><br><p dir="ltr"><span style="font-weight:400;">Второй абзац</span></p></b>`

// A web page: a script, a style, blocks, strong and em, a list, a link with attributes and handlers, a relative link.
export const webHtml = `<div class="article"><script>alert('x')</script><style>.a{color:red}</style>
<h2 id="t">Заголовок <em>статьи</em></h2>
<p onclick="steal()">Текст с <strong>важным</strong> и <a href="https://example.test/" target="_blank" title="t" onclick="x()">ссылкой</a>,
<a href="/relative">относительной</a> и <span style="color:red">цветом</span>.</p>
<ul><li>Пункт один</li><li>Пункт <mark>два</mark></li></ul><iframe srcdoc="x"></iframe></div>`

// Lists as Word puts them on the clipboard (shortened): paragraphs with `mso-list` (the list, its level, its format),
// the marker in a span with `mso-list:Ignore` between conditional comments of the `<![if …]>` form, a bulleted list
// with a nested item and a numbered list after a paragraph.
export const wordListHtml = `<html xmlns:o="urn:schemas-microsoft-com:office:office"><body lang=RU><!--StartFragment-->
<p class=MsoListParagraphCxSpFirst style='text-indent:-18.0pt;mso-list:l0 level1 lfo1'><![if !supportLists]><span
style='font-family:Symbol;mso-fareast-font-family:Symbol'><span style='mso-list:Ignore'>·<span
style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; </span></span></span><![endif]>Первый
<b>пункт</b><o:p></o:p></p>
<p class=MsoListParagraphCxSpMiddle style='margin-left:72.0pt;text-indent:-18.0pt;mso-list:l0 level2 lfo1'><![if !supportLists]><span
style='font-family:"Courier New"'><span style='mso-list:Ignore'>o<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;
</span></span></span><![endif]>Вложенный<o:p></o:p></p>
<p class=MsoListParagraphCxSpLast style='text-indent:-18.0pt;mso-list:l0 level1 lfo1'><![if !supportLists]><span
style='font-family:Symbol'><span style='mso-list:Ignore'>·<span style='font:7.0pt "Times New Roman"'>&nbsp;
</span></span></span><![endif]>Второй<o:p></o:p></p>
<p class=MsoNormal>Между списками<o:p></o:p></p>
<p class=MsoListParagraphCxSpFirst style='text-indent:-18.0pt;mso-list:l1 level1 lfo2'><![if !supportLists]><span
style='mso-list:Ignore'>1.<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp; </span></span><![endif]>Шаг
один<o:p></o:p></p>
<p class=MsoListParagraphCxSpLast style='text-indent:-18.0pt;mso-list:l1 level1 lfo2'><![if !supportLists]><span
style='mso-list:Ignore'>2.<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp; </span></span><![endif]>Шаг
<i>два</i><o:p></o:p></p>
<!--EndFragment--></body></html>`

// A list from Google Docs: a paragraph in each item and the nested list right inside the outer one.
export const googleDocsListHtml = `<meta charset="utf-8"><b style="font-weight:normal;" id="docs-internal-guid-5678"><ol style="margin-top:0;margin-bottom:0;"><li dir="ltr" style="list-style-type:decimal;font-size:11pt;" aria-level="1"><p dir="ltr" role="presentation"><span style="font-weight:400;">Раз</span></p></li><ol style="margin-top:0;"><li dir="ltr" style="list-style-type:lower-alpha;" aria-level="2"><p dir="ltr" role="presentation"><span style="font-weight:700;">Вложенный</span></p></li></ol><li dir="ltr" aria-level="1"><p dir="ltr" role="presentation"><span style="font-weight:400;">Два</span></p></li></ol></b>`
