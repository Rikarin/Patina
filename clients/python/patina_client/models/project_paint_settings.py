from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_brush import StackBrush
  from ..models.stack_symmetry import StackSymmetry





T = TypeVar("T", bound="ProjectPaintSettings")



@_attrs_define
class ProjectPaintSettings:
    """ How a project's painting was left: the colours last used and kept, the brush and the symmetry. A project keeps
    it in its `project.yaml`, so it survives a restart and any client starts from it.

        Attributes:
            recent_colours (list[str]): The colours strokes last used, newest first, as `#rrggbb`.
            palette (list[str]): Colours kept on purpose, in the order they were added, as `#rrggbb`.
            brush (None | StackBrush | Unset):
            symmetry (None | StackSymmetry | Unset):
     """

    recent_colours: list[str]
    palette: list[str]
    brush: None | StackBrush | Unset = UNSET
    symmetry: None | StackSymmetry | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_symmetry import StackSymmetry # noqa: PLC0415
        recent_colours = self.recent_colours



        palette = self.palette



        brush: dict[str, Any] | None | Unset
        if isinstance(self.brush, Unset):
            brush = UNSET
        elif isinstance(self.brush, StackBrush):
            brush = self.brush.to_dict()
        else:
            brush = self.brush

        symmetry: dict[str, Any] | None | Unset
        if isinstance(self.symmetry, Unset):
            symmetry = UNSET
        elif isinstance(self.symmetry, StackSymmetry):
            symmetry = self.symmetry.to_dict()
        else:
            symmetry = self.symmetry


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "recentColours": recent_colours,
            "palette": palette,
        })
        if brush is not UNSET:
            field_dict["brush"] = brush
        if symmetry is not UNSET:
            field_dict["symmetry"] = symmetry

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_brush import StackBrush # noqa: PLC0415
        from ..models.stack_symmetry import StackSymmetry # noqa: PLC0415
        d = dict(src_dict)
        recent_colours = cast(list[str], d.pop("recentColours"))


        palette = cast(list[str], d.pop("palette"))


        def _parse_brush(data: object) -> None | StackBrush | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                brush_type_1 = StackBrush.from_dict(data)



                return brush_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackBrush | Unset, data)

        brush = _parse_brush(d.pop("brush", UNSET))


        def _parse_symmetry(data: object) -> None | StackSymmetry | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                symmetry_type_1 = StackSymmetry.from_dict(data)



                return symmetry_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackSymmetry | Unset, data)

        symmetry = _parse_symmetry(d.pop("symmetry", UNSET))


        project_paint_settings = cls(
            recent_colours=recent_colours,
            palette=palette,
            brush=brush,
            symmetry=symmetry,
        )


        project_paint_settings.additional_properties = d
        return project_paint_settings

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
