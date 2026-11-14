from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.autosave_preferences import AutosavePreferences
  from ..models.bake_options import BakeOptions





T = TypeVar("T", bound="ServerPreferences")



@_attrs_define
class ServerPreferences:
    """ The Server's settings that belong to the person and not to a library or project, kept in a plain file beside
    the discovery file, which anyone can read and edit.

        Attributes:
            autosave (AutosavePreferences): How unsaved work is kept. It goes to the library's cache unless a folder is
                chosen, never over a document.
            bake (BakeOptions | None | Unset):
            cache_budget (int | None | Unset): The most room each library's cache may take, in bytes, or null for no limit
                (#189). Over it, the built
                objects, bakes and reports used longest ago go, as Prune takes them, while nothing is building.
     """

    autosave: AutosavePreferences
    bake: BakeOptions | None | Unset = UNSET
    cache_budget: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.autosave_preferences import AutosavePreferences # noqa: PLC0415
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        autosave = self.autosave.to_dict()

        bake: dict[str, Any] | None | Unset
        if isinstance(self.bake, Unset):
            bake = UNSET
        elif isinstance(self.bake, BakeOptions):
            bake = self.bake.to_dict()
        else:
            bake = self.bake

        cache_budget: int | None | Unset
        if isinstance(self.cache_budget, Unset):
            cache_budget = UNSET
        else:
            cache_budget = self.cache_budget


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "autosave": autosave,
        })
        if bake is not UNSET:
            field_dict["bake"] = bake
        if cache_budget is not UNSET:
            field_dict["cacheBudget"] = cache_budget

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.autosave_preferences import AutosavePreferences # noqa: PLC0415
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        d = dict(src_dict)
        autosave = AutosavePreferences.from_dict(d.pop("autosave"))




        def _parse_bake(data: object) -> BakeOptions | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                bake_type_1 = BakeOptions.from_dict(data)



                return bake_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(BakeOptions | None | Unset, data)

        bake = _parse_bake(d.pop("bake", UNSET))


        def _parse_cache_budget(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        cache_budget = _parse_cache_budget(d.pop("cacheBudget", UNSET))


        server_preferences = cls(
            autosave=autosave,
            bake=bake,
            cache_budget=cache_budget,
        )


        server_preferences.additional_properties = d
        return server_preferences

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
