from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.brush_preset import BrushPreset





T = TypeVar("T", bound="BrushPresetUpdateRequest")



@_attrs_define
class BrushPresetUpdateRequest:
    """ The body of `PUT /api/v1/library/brushes`, which rewrites a preset: from the brush as it stands, or to make
    it a favourite or not. A shipped preset cannot change, so changing one keeps a copy in the project instead.

        Attributes:
            project (str): The project a shipped preset's copy goes in.
            source (str): Which preset, as the shelf names it.
            preset (BrushPreset): A brush or tool preset: what the pointer paints as and with. It is a document of a
                project, a `.ptbrush`
                file, and the file is the truth.
     """

    project: str
    source: str
    preset: BrushPreset
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.brush_preset import BrushPreset # noqa: PLC0415
        project = self.project

        source = self.source

        preset = self.preset.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "source": source,
            "preset": preset,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.brush_preset import BrushPreset # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        source = d.pop("source")

        preset = BrushPreset.from_dict(d.pop("preset"))




        brush_preset_update_request = cls(
            project=project,
            source=source,
            preset=preset,
        )


        brush_preset_update_request.additional_properties = d
        return brush_preset_update_request

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
