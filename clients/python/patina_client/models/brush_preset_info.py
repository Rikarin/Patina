from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.brush_preset import BrushPreset





T = TypeVar("T", bound="BrushPresetInfo")



@_attrs_define
class BrushPresetInfo:
    """ A preset on the shelf.

        Attributes:
            name (str): What it is called.
            source (str): What names it to the routes: `starter:` and a name, or its file's path in the library.
            origin (str): `shipped`, `project`, or `library` for another project.
            preset (BrushPreset): A brush or tool preset: what the pointer paints as and with. It is a document of a
                project, a `.ptbrush`
                file, and the file is the truth.
            project (None | str | Unset): The project it lives in, when that is another one.
     """

    name: str
    source: str
    origin: str
    preset: BrushPreset
    project: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.brush_preset import BrushPreset # noqa: PLC0415
        name = self.name

        source = self.source

        origin = self.origin

        preset = self.preset.to_dict()

        project: None | str | Unset
        if isinstance(self.project, Unset):
            project = UNSET
        else:
            project = self.project


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "source": source,
            "origin": origin,
            "preset": preset,
        })
        if project is not UNSET:
            field_dict["project"] = project

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.brush_preset import BrushPreset # noqa: PLC0415
        d = dict(src_dict)
        name = d.pop("name")

        source = d.pop("source")

        origin = d.pop("origin")

        preset = BrushPreset.from_dict(d.pop("preset"))




        def _parse_project(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        project = _parse_project(d.pop("project", UNSET))


        brush_preset_info = cls(
            name=name,
            source=source,
            origin=origin,
            preset=preset,
            project=project,
        )


        brush_preset_info.additional_properties = d
        return brush_preset_info

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
