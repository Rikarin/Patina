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
  from ..models.export_preset_file import ExportPresetFile





T = TypeVar("T", bound="ExportPresetInfo")



@_attrs_define
class ExportPresetInfo:
    """ An export preset.

        Attributes:
            id (str): Its id, which a build names.
            name (str): Its label.
            engine (str): What it is for.
            normal (str): The green its target reads: y-up or y-down.
            origin (str): Where it comes from: factory, library or project.
            revision (str): The hash of its file.
            files (list[ExportPresetFile]): What it makes.
            padding (ExportPaddingInfo): What fills the texels a mesh's layout leaves empty.
            dither (bool): Whether 8-bit files are dithered.
            filter_ (str): How a file changes size.
            materials (list[str]): The material files it writes beside its textures: gltf, usda or mtlx.
            size (int | None | Unset): The longest side of every file, or null for the maps' own size.
     """

    id: str
    name: str
    engine: str
    normal: str
    origin: str
    revision: str
    files: list[ExportPresetFile]
    padding: ExportPaddingInfo
    dither: bool
    filter_: str
    materials: list[str]
    size: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        from ..models.export_preset_file import ExportPresetFile # noqa: PLC0415
        id = self.id

        name = self.name

        engine = self.engine

        normal = self.normal

        origin = self.origin

        revision = self.revision

        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)



        padding = self.padding.to_dict()

        dither = self.dither

        filter_ = self.filter_

        materials = self.materials



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
            "origin": origin,
            "revision": revision,
            "files": files,
            "padding": padding,
            "dither": dither,
            "filter": filter_,
            "materials": materials,
        })
        if size is not UNSET:
            field_dict["size"] = size

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        from ..models.export_preset_file import ExportPresetFile # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        name = d.pop("name")

        engine = d.pop("engine")

        normal = d.pop("normal")

        origin = d.pop("origin")

        revision = d.pop("revision")

        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = ExportPresetFile.from_dict(files_item_data)



            files.append(files_item)


        padding = ExportPaddingInfo.from_dict(d.pop("padding"))




        dither = d.pop("dither")

        filter_ = d.pop("filter")

        materials = cast(list[str], d.pop("materials"))


        def _parse_size(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        size = _parse_size(d.pop("size", UNSET))


        export_preset_info = cls(
            id=id,
            name=name,
            engine=engine,
            normal=normal,
            origin=origin,
            revision=revision,
            files=files,
            padding=padding,
            dither=dither,
            filter_=filter_,
            materials=materials,
            size=size,
        )


        export_preset_info.additional_properties = d
        return export_preset_info

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
