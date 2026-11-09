from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_options import ExportOptions





T = TypeVar("T", bound="CompressionPreviewRequest")



@_attrs_define
class CompressionPreviewRequest:
    """ The body of `POST /api/v1/export/compression` (#217): one file of an export, to see a region of it as the
    file holds it beside what it was made from.

        Attributes:
            project (str): The project, below `projects/`.
            preset (str): The preset's id.
            file (str): The file, by its template.
            level (int): The mip level, from zero.
            x (float): Where the region is centred across the level, from 0 to 1.
            y (float): Where the region is centred down the level, from 0 to 1.
            side (int): The region's largest side in texels, 4 to 512.
            set_ (None | str | Unset): The texture set, or null for the project's first.
            options (ExportOptions | None | Unset):
     """

    project: str
    preset: str
    file: str
    level: int
    x: float
    y: float
    side: int
    set_: None | str | Unset = UNSET
    options: ExportOptions | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_options import ExportOptions # noqa: PLC0415
        project = self.project

        preset = self.preset

        file = self.file

        level = self.level

        x = self.x

        y = self.y

        side = self.side

        set_: None | str | Unset
        if isinstance(self.set_, Unset):
            set_ = UNSET
        else:
            set_ = self.set_

        options: dict[str, Any] | None | Unset
        if isinstance(self.options, Unset):
            options = UNSET
        elif isinstance(self.options, ExportOptions):
            options = self.options.to_dict()
        else:
            options = self.options


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "preset": preset,
            "file": file,
            "level": level,
            "x": x,
            "y": y,
            "side": side,
        })
        if set_ is not UNSET:
            field_dict["set"] = set_
        if options is not UNSET:
            field_dict["options"] = options

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_options import ExportOptions # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        preset = d.pop("preset")

        file = d.pop("file")

        level = d.pop("level")

        x = d.pop("x")

        y = d.pop("y")

        side = d.pop("side")

        def _parse_set_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        set_ = _parse_set_(d.pop("set", UNSET))


        def _parse_options(data: object) -> ExportOptions | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                options_type_1 = ExportOptions.from_dict(data)



                return options_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(ExportOptions | None | Unset, data)

        options = _parse_options(d.pop("options", UNSET))


        compression_preview_request = cls(
            project=project,
            preset=preset,
            file=file,
            level=level,
            x=x,
            y=y,
            side=side,
            set_=set_,
            options=options,
        )


        compression_preview_request.additional_properties = d
        return compression_preview_request

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
