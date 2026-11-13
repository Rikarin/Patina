from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.export_preset_list import ExportPresetList
from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    project: str | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["project"] = project


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/export/presets",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ExportPresetList | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = ExportPresetList.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ExportPresetList | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str | Unset = UNSET,

) -> Response[ExportPresetList | ProblemDetails]:
    """ Every preset this library can use: the Starter ones and those in shared/export-presets.

    Args:
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ExportPresetList | ProblemDetails]
     """


    kwargs = _get_kwargs(
        project=project,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    project: str | Unset = UNSET,

) -> ExportPresetList | ProblemDetails | None:
    """ Every preset this library can use: the Starter ones and those in shared/export-presets.

    Args:
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ExportPresetList | ProblemDetails
     """


    return sync_detailed(
        client=client,
project=project,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str | Unset = UNSET,

) -> Response[ExportPresetList | ProblemDetails]:
    """ Every preset this library can use: the Starter ones and those in shared/export-presets.

    Args:
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ExportPresetList | ProblemDetails]
     """


    kwargs = _get_kwargs(
        project=project,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    project: str | Unset = UNSET,

) -> ExportPresetList | ProblemDetails | None:
    """ Every preset this library can use: the Starter ones and those in shared/export-presets.

    Args:
        project (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ExportPresetList | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
project=project,

    )).parsed
