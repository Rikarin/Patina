from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.bake_preview_part import BakePreviewPart
  from ..models.viewport_bake_preview import ViewportBakePreview





T = TypeVar("T", bound="BakePreviewState")



@_attrs_define
class BakePreviewState:
    """ What the viewport shows of a bake's setup: the key to its colours, and what it could not show.

        Attributes:
            parts (list[BakePreviewPart]): The parts by name, in the low mesh's order and then the high's, when parts are
                matched by name; empty when
                they are not.
            warnings (list[str]): What the preview left out or could not read.
            preview (None | Unset | ViewportBakePreview):
     """

    parts: list[BakePreviewPart]
    warnings: list[str]
    preview: None | Unset | ViewportBakePreview = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_preview_part import BakePreviewPart # noqa: PLC0415
        from ..models.viewport_bake_preview import ViewportBakePreview # noqa: PLC0415
        parts = []
        for parts_item_data in self.parts:
            parts_item = parts_item_data.to_dict()
            parts.append(parts_item)



        warnings = self.warnings



        preview: dict[str, Any] | None | Unset
        if isinstance(self.preview, Unset):
            preview = UNSET
        elif isinstance(self.preview, ViewportBakePreview):
            preview = self.preview.to_dict()
        else:
            preview = self.preview


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "parts": parts,
            "warnings": warnings,
        })
        if preview is not UNSET:
            field_dict["preview"] = preview

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_preview_part import BakePreviewPart # noqa: PLC0415
        from ..models.viewport_bake_preview import ViewportBakePreview # noqa: PLC0415
        d = dict(src_dict)
        parts = []
        _parts = d.pop("parts")
        for parts_item_data in (_parts):
            parts_item = BakePreviewPart.from_dict(parts_item_data)



            parts.append(parts_item)


        warnings = cast(list[str], d.pop("warnings"))


        def _parse_preview(data: object) -> None | Unset | ViewportBakePreview:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                preview_type_1 = ViewportBakePreview.from_dict(data)



                return preview_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportBakePreview, data)

        preview = _parse_preview(d.pop("preview", UNSET))


        bake_preview_state = cls(
            parts=parts,
            warnings=warnings,
            preview=preview,
        )


        bake_preview_state.additional_properties = d
        return bake_preview_state

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
