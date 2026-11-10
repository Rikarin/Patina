from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_settings_op import StackCommandStackSetSettingsOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackSetSettings")



@_attrs_define
class StackCommandStackSetSettings:
    """ Changes the texture set's settings, any of them, as one undo step. What is left out stays as it is. Together
    with its channels and its name these are the set's settings, and the stack file is the one place they live.

        Attributes:
            op (StackCommandStackSetSettingsOp):
            width (int | None | Unset): The width the set is painted, evaluated and exported at, one of the sides the system
                offers. A canvas painted at
                another resolution is never rewritten by this: it is sampled at the set's, and a smaller one is enlarged exactly
                when it is next painted (#19).
            height (int | None | Unset): The height, which may differ from the width.
            uv_set (int | None | Unset): The mesh's UV set it is painted, baked and drawn in, by its number; 0 is the first.
            height_normal (None | str | Unset): How height becomes the normal: `Sharp` or `Smooth`.
            baked_normal (None | str | Unset): How the bake's normal reaches the set's (#86): `Combine`, beneath everything
                painted; `Replace`, where
                the normal channel starts, so paint covers it; or `Off`.
            baked_occlusion (None | str | Unset): How the bake's ambient occlusion reaches the set's: `Combine`, multiplied
                with what is painted;
                `Replace`; or `Off`.
     """

    op: StackCommandStackSetSettingsOp
    width: int | None | Unset = UNSET
    height: int | None | Unset = UNSET
    uv_set: int | None | Unset = UNSET
    height_normal: None | str | Unset = UNSET
    baked_normal: None | str | Unset = UNSET
    baked_occlusion: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        width: int | None | Unset
        if isinstance(self.width, Unset):
            width = UNSET
        else:
            width = self.width

        height: int | None | Unset
        if isinstance(self.height, Unset):
            height = UNSET
        else:
            height = self.height

        uv_set: int | None | Unset
        if isinstance(self.uv_set, Unset):
            uv_set = UNSET
        else:
            uv_set = self.uv_set

        height_normal: None | str | Unset
        if isinstance(self.height_normal, Unset):
            height_normal = UNSET
        else:
            height_normal = self.height_normal

        baked_normal: None | str | Unset
        if isinstance(self.baked_normal, Unset):
            baked_normal = UNSET
        else:
            baked_normal = self.baked_normal

        baked_occlusion: None | str | Unset
        if isinstance(self.baked_occlusion, Unset):
            baked_occlusion = UNSET
        else:
            baked_occlusion = self.baked_occlusion


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
        })
        if width is not UNSET:
            field_dict["width"] = width
        if height is not UNSET:
            field_dict["height"] = height
        if uv_set is not UNSET:
            field_dict["uvSet"] = uv_set
        if height_normal is not UNSET:
            field_dict["heightNormal"] = height_normal
        if baked_normal is not UNSET:
            field_dict["bakedNormal"] = baked_normal
        if baked_occlusion is not UNSET:
            field_dict["bakedOcclusion"] = baked_occlusion

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetSettingsOp(d.pop("op"))




        def _parse_width(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        width = _parse_width(d.pop("width", UNSET))


        def _parse_height(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        height = _parse_height(d.pop("height", UNSET))


        def _parse_uv_set(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        uv_set = _parse_uv_set(d.pop("uvSet", UNSET))


        def _parse_height_normal(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        height_normal = _parse_height_normal(d.pop("heightNormal", UNSET))


        def _parse_baked_normal(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        baked_normal = _parse_baked_normal(d.pop("bakedNormal", UNSET))


        def _parse_baked_occlusion(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        baked_occlusion = _parse_baked_occlusion(d.pop("bakedOcclusion", UNSET))


        stack_command_stack_set_settings = cls(
            op=op,
            width=width,
            height=height,
            uv_set=uv_set,
            height_normal=height_normal,
            baked_normal=baked_normal,
            baked_occlusion=baked_occlusion,
        )


        stack_command_stack_set_settings.additional_properties = d
        return stack_command_stack_set_settings

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
