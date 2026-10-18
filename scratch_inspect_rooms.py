import os, re, glob

room_names = [
    'B1', 'B2', 'B3', 'B4_Corridor', 'B5', 'B6_Corridor', 'B7_Crossroad',
    'S2_Safe', 'S3', 'S4', 'S5', 'Start', 'Van_Room'
]

print(f"{'Room':<15} | {'Sockets':<8} | {'Socket Names':<45} | {'BoxCollider Size'}")
print("-" * 100)

for r in room_names:
    path = os.path.join("Assets", "Rooms", f"{r}.prefab")
    if not os.path.exists(path):
        path = os.path.join("Assets", f"{r}.prefab")
    if not os.path.exists(path):
        print(f"{r:<15} | {'NOT FOUND':<8}")
        continue
    
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()
    
    # find sockets
    sockets = re.findall(r"m_Name:\s*(Socket[^\r\n]*)", content)
    
    # find box collider size
    box_size_match = re.search(r"BoxCollider:.*?m_Size:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^\}]+)\}", content, re.DOTALL)
    box_size = f"({box_size_match.group(1)}, {box_size_match.group(2)}, {box_size_match.group(3)})" if box_size_match else "NONE"
    
    print(f"{r:<15} | {len(sockets):<8} | {', '.join(sockets):<45} | {box_size}")
