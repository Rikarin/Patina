from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_format_info import ExportFormatInfo
  from ..models.export_preset_info import ExportPresetInfo
  from ..models.stack_term import StackTerm





T = TypeVar("T", bound="ExportPresetList")



@_attrs_define
class ExportPresetList:
    """ Every preset a library can use, and the files in its folder that are not presets.

        Attributes:
            presets (list[ExportPresetInfo]): The presets.
            problems (list[str]): One line per file that is not a preset.
            formats (list[ExportFormatInfo]): The file formats there are, for a client that offers another one.
            paddings (list[StackTerm]): The padding modes there are, in order, each with what a person calls it.
            filters (list[StackTerm]): The resize filters there are, each with what a person calls it.
            compressions (list[StackTerm]): What a KTX2 or DDS file can hold, in order, each with what a person calls it.
            maps (list[StackTerm]): The maps a lane can read, each with what a person calls it.
            materials (list[StackTerm]): The material files a preset can write beside its textures, each with what a person
                calls it.
            tokens (list[str]): The names a file's template can hold, such as `project` for `{project}`.
            default (None | str | Unset): The preset an export starts from when nothing is kept for it: the library's
                `defaultPreset` while it
                names one of these, else the factory's glTF starter, and null only when there are no presets at all.
     """

    presets: list[ExportPresetInfo]
    problems: list[str]
    formats: list[ExportFormatInfo]
    paddings: list[StackTerm]
    filters: list[StackTerm]
    compressions: list[StackTerm]
    maps: list[StackTerm]
    materials: list[StackTerm]
    tokens: list[str]
    default: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_format_info import ExportFormatInfo # noqa: PLC0415
        from ..models.export_preset_info import ExportPresetInfo # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        presets = []
        for presets_item_data in self.presets:
            presets_item = presets_item_data.to_dict()
            presets.append(presets_item)



        problems = self.problems



        formats = []
        for formats_item_data in self.formats:
            formats_item = formats_item_data.to_dict()
            formats.append(formats_item)



        paddings = []
        for paddings_item_data in self.paddings:
            paddings_item = paddings_item_data.to_dict()
            paddings.append(paddings_item)



        filters = []
        for filters_item_data in self.filters:
            filters_item = filters_item_data.to_dict()
            filters.append(filters_item)



        compressions = []
        for compressions_item_data in self.compressions:
            compressions_item = compressions_item_data.to_dict()
            compressions.append(compressions_item)



        maps = []
        for maps_item_data in self.maps:
            maps_item = maps_item_data.to_dict()
            maps.append(maps_item)



        materials = []
        for materials_item_data in self.materials:
            materials_item = materials_item_data.to_dict()
            materials.append(materials_item)



        tokens = self.tokens



        default: None | str | Unset
        if isinstance(self.default, Unset):
            default = UNSET
        else:
            default = self.default


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "presets": presets,
            "problems": problems,
            "formats": formats,
            "paddings": paddings,
            "filters": filters,
            "compressions": compressions,
            "maps": maps,
            "materials": materials,
            "tokens": tokens,
        })
        if default is not UNSET:
            field_dict["default"] = default

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_format_info import ExportFormatInfo # noqa: PLC0415
        from ..models.export_preset_info import ExportPresetInfo # noqa: PLC0415
        from ..models.stack_term import StackTerm # noqa: PLC0415
        d = dict(src_dict)
        presets = []
        _presets = d.pop("presets")
        for presets_item_data in (_presets):
            presets_item = ExportPresetInfo.from_dict(presets_item_data)



            presets.append(presets_item)


        problems = cast(list[str], d.pop("problems"))


        formats = []
        _formats = d.pop("formats")
        for formats_item_data in (_formats):
            formats_item = ExportFormatInfo.from_dict(formats_item_data)



            formats.append(formats_item)


        paddings = []
        _paddings = d.pop("paddings")
        for paddings_item_data in (_paddings):
            paddings_item = StackTerm.from_dict(paddings_item_data)



            paddings.append(paddings_item)


        filters = []
        _filters = d.pop("filters")
        for filters_item_data in (_filters):
            filters_item = StackTerm.from_dict(filters_item_data)



            filters.append(filters_item)


        compressions = []
        _compressions = d.pop("compressions")
        for compressions_item_data in (_compressions):
            compressions_item = StackTerm.from_dict(compressions_item_data)



            compressions.append(compressions_item)


        maps = []
        _maps = d.pop("maps")
        for maps_item_data in (_maps):
            maps_item = StackTerm.from_dict(maps_item_data)



            maps.append(maps_item)


        materials = []
        _materials = d.pop("materials")
        for materials_item_data in (_materials):
            materials_item = StackTerm.from_dict(materials_item_data)



            materials.append(materials_item)


        tokens = cast(list[str], d.pop("tokens"))


        def _parse_default(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        default = _parse_default(d.pop("default", UNSET))


        export_preset_list = cls(
            presets=presets,
            problems=problems,
            formats=formats,
            paddings=paddings,
            filters=filters,
            compressions=compressions,
            maps=maps,
            materials=materials,
            tokens=tokens,
            default=default,
        )


        export_preset_list.additional_properties = d
        return export_preset_list

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
