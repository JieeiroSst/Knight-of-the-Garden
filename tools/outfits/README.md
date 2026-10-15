# Bo giap Trung co - cach tao lai assets3d/outfits/

Nguon (tai tu poly.pizza):
- King - Quaternius, CC0: https://poly.pizza/m/I1gTjmuK2m -> `king.glb`
- Helmet - CreativeTechLab, CC-BY 3.0: https://poly.pizza/m/uSCGe4LD2v -> `helmet.glb`

Can Python 3 + numpy + Pillow.

```sh
C=assets3d/quaternius/characters
# Nam: chuyen giap King sang bo xuong Farmer (dung chung cho moi nhan vat nam), mu tru buoc vao xuong Head.
python3 tools/outfits/outfit_bake.py king.glb $C/Farmer.gltf assets3d/outfits/knight_m.glb male King_Body,King_Legs,King_Feet "helmet.glb:1.05:0,1.688,-0.02:180"
# Nu: anh xa xuong nam -> nu, noi rong 1.25x theo chieu ngang, bo luoi tay "Skin" (giu tay nhan vat nu).
python3 tools/outfits/outfit_bake.py king.glb $C/Woman_Casual.glb assets3d/outfits/knight_f.glb female King_Body,King_Legs,King_Feet "helmet.glb:2.25:0,4.268,-0.111:180" Skin 1.25
# Xem truoc (anh PNG mat truoc + mat ben), co the dung animation cua nhan vat goc:
python3 tools/outfits/glbview.py preview.png assets3d/outfits/knight_m.glb --anim Idle --animfrom $C/Farmer.gltf --also $C/Casual_2.gltf:Casual2_Head
```

## Nhan vat Trung co (ghep dau/than)
```sh
O=assets3d/outfits
# Doi ten animation "CharacterArmature|Idle" khong can - chi lay luoi, bo xuong + hoat canh lay tu Farmer.gltf.
python3 tools/outfits/outfit_bake.py King.glb $C/Farmer.gltf $O/king_parts.glb male King_Body,King_Legs,King_Feet,King_Head
python3 tools/outfits/outfit_bake.py Hooded.glb $C/Farmer.gltf $O/hooded_parts.glb male Medieval_Body,Medieval_Legs,Medieval_Feet,Medieval_Head
python3 tools/outfits/outfit_bake.py AdventurerF.glb $C/Farmer.gltf $O/adventurerf_parts.glb male Adventurer_Body,Adventurer_Legs,Adventurer_Feet,Adventurer_Head
```
