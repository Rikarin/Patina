from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_preview import StackPreview





T = TypeVar("T", bound="StackEvaluation")



@_attrs_define
class StackEvaluation:
    """ How an evaluation went.

        Attributes:
            revision (int): The revision it was of.
            compiled (bool): Whether the plan had to compile.
            ran (int): How many ops ran.
            repaired (int): How many of those ran over the tiles a stroke changed rather than the whole image.
            reused (int): How many came from the cache.
            milliseconds (float): How long the Worker took.
            previews (list[StackPreview]): The maps that changed.
            diagnostics (list[str]): Why it could not evaluate, or what it warned about.
            error (None | str | Unset): Why the evaluation failed, or null.
     """

    revision: int
    compiled: bool
    ran: int
    repaired: int
    reused: int
    milliseconds: float
    previews: list[StackPreview]
    diagnostics: list[str]
    error: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_preview import StackPreview # noqa: PLC0415
        revision = self.revision

        compiled = self.compiled

        ran = self.ran

        repaired = self.repaired

        reused = self.reused

        milliseconds = self.milliseconds

        previews = []
        for previews_item_data in self.previews:
            previews_item = previews_item_data.to_dict()
            previews.append(previews_item)



        diagnostics = self.diagnostics



        error: None | str | Unset
        if isinstance(self.error, Unset):
            error = UNSET
        else:
            error = self.error


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "revision": revision,
            "compiled": compiled,
            "ran": ran,
            "repaired": repaired,
            "reused": reused,
            "milliseconds": milliseconds,
            "previews": previews,
            "diagnostics": diagnostics,
        })
        if error is not UNSET:
            field_dict["error"] = error

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_preview import StackPreview # noqa: PLC0415
        d = dict(src_dict)
        revision = d.pop("revision")

        compiled = d.pop("compiled")

        ran = d.pop("ran")

        repaired = d.pop("repaired")

        reused = d.pop("reused")

        milliseconds = d.pop("milliseconds")

        previews = []
        _previews = d.pop("previews")
        for previews_item_data in (_previews):
            previews_item = StackPreview.from_dict(previews_item_data)



            previews.append(previews_item)


        diagnostics = cast(list[str], d.pop("diagnostics"))


        def _parse_error(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        error = _parse_error(d.pop("error", UNSET))


        stack_evaluation = cls(
            revision=revision,
            compiled=compiled,
            ran=ran,
            repaired=repaired,
            reused=reused,
            milliseconds=milliseconds,
            previews=previews,
            diagnostics=diagnostics,
            error=error,
        )


        stack_evaluation.additional_properties = d
        return stack_evaluation

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
