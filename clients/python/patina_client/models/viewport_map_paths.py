from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ViewportMapPaths")



@_attrs_define
class ViewportMapPaths:
    """ Pictures to lay over the mesh's own materials, on every part. Each is a path on the Server's machine, and a slot
    left null keeps what the material asks for. A filled slot replaces the material's factor too, so what shows
    is the picture. Roughness is read from green, metallic from blue and occlusion from red, so one packed file or
    three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as linear.

        Attributes:
            base_color (None | str | Unset): The base colour.
            normal (None | str | Unset): A tangent-space normal map in MikkTSpace, with green up the image.
            roughness (None | str | Unset): Perceptual roughness.
            metallic (None | str | Unset): How metallic it is.
            occlusion (None | str | Unset): Ambient occlusion.
            look (None | str | Unset): The viewing table, which `PUT /viewport/look` sets and pictures do not touch.
            emissive (None | str | Unset): Light the surface gives off.
            opacity (None | str | Unset): What the surface covers, read from red and shown as cut out or not.
     """

    base_color: None | str | Unset = UNSET
    normal: None | str | Unset = UNSET
    roughness: None | str | Unset = UNSET
    metallic: None | str | Unset = UNSET
    occlusion: None | str | Unset = UNSET
    look: None | str | Unset = UNSET
    emissive: None | str | Unset = UNSET
    opacity: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        base_color: None | str | Unset
        if isinstance(self.base_color, Unset):
            base_color = UNSET
        else:
            base_color = self.base_color

        normal: None | str | Unset
        if isinstance(self.normal, Unset):
            normal = UNSET
        else:
            normal = self.normal

        roughness: None | str | Unset
        if isinstance(self.roughness, Unset):
            roughness = UNSET
        else:
            roughness = self.roughness

        metallic: None | str | Unset
        if isinstance(self.metallic, Unset):
            metallic = UNSET
        else:
            metallic = self.metallic

        occlusion: None | str | Unset
        if isinstance(self.occlusion, Unset):
            occlusion = UNSET
        else:
            occlusion = self.occlusion

        look: None | str | Unset
        if isinstance(self.look, Unset):
            look = UNSET
        else:
            look = self.look

        emissive: None | str | Unset
        if isinstance(self.emissive, Unset):
            emissive = UNSET
        else:
            emissive = self.emissive

        opacity: None | str | Unset
        if isinstance(self.opacity, Unset):
            opacity = UNSET
        else:
            opacity = self.opacity


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if base_color is not UNSET:
            field_dict["baseColor"] = base_color
        if normal is not UNSET:
            field_dict["normal"] = normal
        if roughness is not UNSET:
            field_dict["roughness"] = roughness
        if metallic is not UNSET:
            field_dict["metallic"] = metallic
        if occlusion is not UNSET:
            field_dict["occlusion"] = occlusion
        if look is not UNSET:
            field_dict["look"] = look
        if emissive is not UNSET:
            field_dict["emissive"] = emissive
        if opacity is not UNSET:
            field_dict["opacity"] = opacity

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_base_color(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        base_color = _parse_base_color(d.pop("baseColor", UNSET))


        def _parse_normal(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        normal = _parse_normal(d.pop("normal", UNSET))


        def _parse_roughness(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        roughness = _parse_roughness(d.pop("roughness", UNSET))


        def _parse_metallic(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        metallic = _parse_metallic(d.pop("metallic", UNSET))


        def _parse_occlusion(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        occlusion = _parse_occlusion(d.pop("occlusion", UNSET))


        def _parse_look(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        look = _parse_look(d.pop("look", UNSET))


        def _parse_emissive(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        emissive = _parse_emissive(d.pop("emissive", UNSET))


        def _parse_opacity(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        opacity = _parse_opacity(d.pop("opacity", UNSET))


        viewport_map_paths = cls(
            base_color=base_color,
            normal=normal,
            roughness=roughness,
            metallic=metallic,
            occlusion=occlusion,
            look=look,
            emissive=emissive,
            opacity=opacity,
        )


        viewport_map_paths.additional_properties = d
        return viewport_map_paths

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
