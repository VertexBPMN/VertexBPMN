import re, glob, os
pages_dir = "src/VertexBPMN.Studio/Components/Pages"
print("route\tpage\th1\h4\pagetitle")
for f in sorted(glob.glob(os.path.join(pages_dir, "*.razor"))):
    txt = open(f, encoding="utf-8").read()
    route = re.search(r'@page\s+"([^"]+)"', txt)
    if not route:
        continue
    # candidates in priority order
    h1 = re.findall(r'<MudText[^>]*Typo="Typo\.h1"[^>]*>([^<{]+)<', txt)
    h4 = re.findall(r'<MudText[^>]*Typo="Typo\.h4"[^>]*>([^<{]+)<', txt)
    h5 = re.findall(r'<MudText[^>]*Typo="Typo\.h5"[^>]*>([^<{]+)<', txt)
    h6 = re.findall(r'<MudText[^>]*Typo="Typo\.h6"|Typo="Typography' , txt)
    pt = re.findall(r'<PageTitle[^>]*>([^<]+)<', txt)
    html_h = re.findall(r'<h([123456])[^>]*>\s*([^<{]+)<', txt)
    cand = (h1[:1], h4[:1], html_h[:1], pt[:1])
    print(f"{route.group(1)}\t{os.path.basename(f)}\t{h1[:1]}\t{h4[:1]}\thtml_h={html_h[:1]}\tpagetitle={pt[:1]}")
