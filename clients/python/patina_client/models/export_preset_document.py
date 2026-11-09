from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_padding_info import ExportPaddingInfo
  from ..models.export_preset_texture_document import ExportPresetTextureDocument





T = TypeVar("T", bound="ExportPresetDocument")



@_attrs_define
class ExportPresetDocument:
    """ A preset as its file says it, which is what the preset editor reads and writes (ADR 0019).

        Attributes:
            id (str): Its id: lower-case letters, digits and hyphens. It is also the file's name.
            name (str): Its label.
            engine (str): What it is for.
            normal (str): The green its target reads: y-up or y-down.
            filter_ (str): How a file changes size: box, bilinear or nearest.
            dither (bool): Whether 8-bit files are dithered.
            padding (ExportPaddingInfo): What fills the texels a mesh's layout leaves empty.
            materials (list[str]): The material files written beside the textures: gltf, usda or mtlx.
            textures (list[ExportPresetTextureDocument]): The files.
            size (int | None | Unset): The longest side of every file, or null for the maps' own size.
     """

    id: str
    name: str
    engine: str
    normal: str
    filter_: str
    dither: bool
    padding: ExportPaddingInfo
    materials: list[str]
    textures: list[ExportPresetTextureDocument]
    size: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        from ..models.export_preset_texture_document import ExportPresetTextureDocument # noqa: PLC0415
        id = self.id

        name = self.name

        engine = self.engine

        normal = self.normal

        filter_ = self.filter_

        dither = self.dither

        padding = self.padding.to_dict()

        materials = self.materials



        textures = []
        for textures_item_data in self.textures:
            textures_item = textures_item_data.to_dict()
            textures.append(textures_item)



        size: int | None | Unset
        if isinstance(self.size, Unset):
            size = UNSET
        else:
            size = self.size


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "engine": engine,
            "normal": normal,
            "filter": filter_,
            "dither": dither,
            "padding": padding,
            "materials": materials,
            "textures": textures,
        })
        if size is not UNSET:
            field_dict["size"] = size

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        from ..models.export_preset_texture_document import ExportPresetTextureDocument # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        name = d.pop("name")

        engine = d.pop("engine")

        normal = d.pop("normal")

        filter_ = d.pop("filter")

        dither = d.pop("dither")

        padding = ExportPaddingInfo.from_dict(d.pop("padding"))




        materials = cast(list[str], d.pop("materials"))


        textures = []
        _textures = d.pop("textures")
        for textures_item_data in (_textures):
            textures_item = ExportPresetTextureDocument.from_dict(textures_item_data)



            textures.append(textures_item)


        def _parse_size(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        size = _parse_size(d.pop("size", UNSET))


        export_preset_document = cls(
            id=id,
            name=name,
            engine=engine,
            normal=normal,
            filter_=filter_,
            dither=dither,
            padding=padding,
            materials=materials,
            textures=textures,
            size=size,
        )


        export_preset_document.additional_properties = d
        return export_preset_document

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
