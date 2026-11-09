from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.check_problem_values import CheckProblemValues
  from ..models.check_target import CheckTarget





T = TypeVar("T", bound="CheckProblem")



@_attrs_define
class CheckProblem:
    """ One problem a texture set has before it is exported.

        Attributes:
            code (str): One of CheckCodes.
            severity (str): `error` stops an engine taking it; `warning` needs a look.
            detail (str): The Server's own sentence.
            values (CheckProblemValues): The numbers and names the sentence was written from, by name.
            target (CheckTarget): Where a problem is, for a client to go to it.
     """

    code: str
    severity: str
    detail: str
    values: CheckProblemValues
    target: CheckTarget
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.check_problem_values import CheckProblemValues # noqa: PLC0415
        from ..models.check_target import CheckTarget # noqa: PLC0415
        code = self.code

        severity = self.severity

        detail = self.detail

        values = self.values.to_dict()

        target = self.target.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "code": code,
            "severity": severity,
            "detail": detail,
            "values": values,
            "target": target,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.check_problem_values import CheckProblemValues # noqa: PLC0415
        from ..models.check_target import CheckTarget # noqa: PLC0415
        d = dict(src_dict)
        code = d.pop("code")

        severity = d.pop("severity")

        detail = d.pop("detail")

        values = CheckProblemValues.from_dict(d.pop("values"))




        target = CheckTarget.from_dict(d.pop("target"))




        check_problem = cls(
            code=code,
            severity=severity,
            detail=detail,
            values=values,
            target=target,
        )


        check_problem.additional_properties = d
        return check_problem

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
